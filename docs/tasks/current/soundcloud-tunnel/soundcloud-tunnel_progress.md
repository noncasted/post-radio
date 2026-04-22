# SoundCloud tunnel — Рабочие заметки

## Статус: В работе (разделение сессии)

## Выполнено в этой сессии

- [x] Понимание проблемы: РФ ISP режет SC через DPI (TSPU); под VPN всё работает
- [x] Реализация выключателя `Audio.Socks5Proxy` и общего HttpClient-handler
      в `AudioServicesExtensions` / `AudioOptions`
- [x] Ручной SOCKS5 клиент в `Socks5ConnectCallback.cs` (.NET-ский WebProxy
      с SOCKS5 оказался ненадёжным — написал поверх `ConnectCallback`)
- [x] Подробное диагностическое логирование в `AudioServicesStartup`,
      `PlaylistLoader.Fetch`, handler factory, Aspire `Program.cs`
- [x] Попытка 1: SSH tunnel (`autossh -D`) → DPI режет sustained SSH → фейл
- [x] Попытка 2: shadowsocks-libev (`chacha20-ietf-poly1305`) на порту 8443 —
      TCP проходит, но TSPU троттлит до 120 B/сек → фейл
- [x] Попытка 3: Xray VLESS+Reality+Vision (серверный Xray установлен
      по `/usr/local/bin/xray`, порт 8443) → тоже режется на handshake → фейл
- [x] Попытка 4: AmneziaWG через userspace `amneziawg-go` в sidecar-контейнере.
      Handshake к серверу `amnezia-awg2:31748` отправляется (534 B sent), но
      сервер не отвечает (0 B received). GUI-клиент AmneziaVPN на той же
      машине с ровно тем же конфигом handshake проходит (сервер показывает
      `post-radio-tunnel` peer: `Latest handshake: 3m 52s ago, 141 KiB received`).
- [x] Гипотеза: amneziawg-go (master или v0.2.12) не полностью реализует
      протокол AmneziaWG v2 (S3/S4/I1 support в парсере есть, но на проводе
      вероятно отличается от server expectation)
- [x] Инфраструктурные артефакты (для последующего cleanup):
      - VPS: shadowsocks-libev systemd unit, Xray systemd unit, sshd порт 8443,
        отдельный AmneziaVPN peer `post-radio-tunnel`, отдельная ssh-ключпара
        `sc-tunnel-20260421`
      - Репо: `backend/Tools/deploy/` со всей инфраструктурой sidecar,
        `backend/Meta/Audio/Socks5ConnectCallback.cs`, диагностические логи
        в нескольких файлах, гитигнор `**/secrets.local.json`, `**/secrets/`
- [x] Принято решение: **хостить прод в Казахстане**, tunneling не нужен

## Текущий момент остановки

Сессия заканчивается решением переехать на KZ-хостинг. Следующая сессия
должна **удалить весь код и артефакты тоннелирования** по списку ниже.
Бэкенд должен работать с `Audio.Socks5Proxy = null` напрямую (из KZ-хоста
SC доступен).

**Ничего не ломаем в активном состоянии** — на момент разделения сессии:
- `appsettings.local.json` (Aspire) уже возвращён в чистый вид (секция
  `Audio` удалена) — в deve проходит напрямую при включённой AmneziaVPN
- `AudioServicesExtensions` создаёт handler без прокси когда `Socks5Proxy` пуст,
  работает корректно
- Sidecar-контейнер (`sc-tunnel`) можно оставить не-запущенным, compose
  не интегрирован с остальным стеком → на работу бэкенда не влияет
- На VPS сервисы shadowsocks / xray активны, но никто к ним не ходит
  (dev-клиент перешёл на AmneziaVPN GUI)

## Что делать в следующей сессии

1. Прочитать `soundcloud-tunnel_info.md` — там полный список файлов и
   состояний
2. Выполнить cleanup по списку из таблицы файлов в `_info.md`:
   - Удалить `backend/Meta/Audio/Socks5ConnectCallback.cs`
   - Выкинуть диагностические логи из `AudioServicesStartup`, `PlaylistLoader`,
     `AudioServicesExtensions`, Aspire `Program.cs`
   - Сохранить `AudioOptions.Socks5Proxy` как дремлющий выключатель
     (null-by-default) и минималистичную логику в `CreateSoundCloudHandler`
     (если Socks5Proxy не задан — просто `new SocketsHttpHandler()`, без ILogger)
   - Удалить `backend/Tools/deploy/sc-tunnel/`,
     `backend/Tools/deploy/docker-compose.yaml`,
     `backend/Tools/deploy/.env.example`,
     `backend/Tools/deploy/secrets/`
   - Вычистить `backend/Tools/DeploySetup/secrets.local.json` (оставить
     только `ScTunnel.Host/User/Password` если нужны для админ-доступа, либо
     удалить целиком)
   - Пересобрать `backend/post-radio.slnx` — должно собраться без предупреждений
3. Обновить `backend/Tools/deploy/README.md` — описать новую схему
   (прод в KZ, тоннель не требуется; или удалить README и сам deploy/
   целиком если там больше ничего нет)
4. На VPS (`51.68.143.178`) вычистить:
   - `sudo systemctl disable --now shadowsocks-libev-server@post-radio.service`
   - `sudo rm /etc/shadowsocks-libev/post-radio.json`
   - `sudo systemctl disable --now xray && sudo rm -rf /usr/local/bin/xray /usr/local/etc/xray /var/log/xray /etc/systemd/system/xray*.service`
   - `sudo sed -i '/^Port 8443$/d' /etc/ssh/sshd_config && sudo systemctl reload ssh.socket`
   - В `~ubuntu/.ssh/authorized_keys` убрать строку с comment `sc-tunnel-20260421`
     и удалить `~ubuntu/.ssh/sc_tunnel_key{,.pub}`
   - В AmneziaVPN админ-панели удалить peer `post-radio-tunnel`
5. Убедиться что `aspire run` поднимает стек без ошибок и бэкенд успешно
   подключается к SoundCloud (с включённой на дев-машине AmneziaVPN — для деdev)

## Важные находки из сессии

- `Audio.Socks5Proxy` должен прокидываться во все cluster-participant сервисы
  (silo/coordinator/meta/console), а не только в silo, потому что
  `AddAudioServices` подключается в `AddBase` для всех
- В .NET 10 `SocketsHttpHandler.Proxy = new WebProxy("socks5://...")` на деле
  не инициирует SOCKS5 handshake корректно — `ConnectCallback` надёжнее
- `amneziawg-tools` master ветки корректно парсит AmneziaWG v2 конфиг
  (`S3/S4/I1/H-ranges`), тег `v1.0.20241018` — нет
- `amneziawg-go` и master, и тег `v0.2.12` отправляют handshake, но сервер
  не отвечает (`0 B received` в `awg show`)
- Docker-compose `network_mode: host` handshake не чинит — проблема не
  в NAT/conntrack
- SSH через ssh.socket override-юнит может сломаться так, что sshd не
  поднимается (восстанавливал через OVH KVM rescue mode)

## Изменённые файлы на момент разделения

Вся работа лежит в untracked `backend/`. Конкретные файлы которые трогали:

| Файл | Изменено |
|------|----------|
| `backend/Meta/Audio/AudioOptions.cs` | добавлено поле `Socks5Proxy` |
| `backend/Meta/Audio/AudioServicesExtensions.cs` | named HttpClient, прокси-handler, диагностика |
| `backend/Meta/Audio/AudioServicesStartup.cs` | try/catch + логи вокруг `InitializeAsync` |
| `backend/Meta/Audio/Playlists/PlaylistLoader.cs` | try/catch + логи вокруг `GetTracksAsync` |
| `backend/Meta/Audio/Socks5ConnectCallback.cs` | **новый файл**, ручной SOCKS5 клиент с логами |
| `backend/Orchestration/Aspire/Program.cs` | форвард `Audio__Socks5Proxy` во все проекты |
| `backend/Orchestration/Aspire/appsettings.local.json` | (чистый на момент раздела, `Audio` секция удалена) |
| `backend/Tools/DeploySetup/secrets.local.json` | **новый файл** с кредами всех попыток (ScTunnel, Shadowsocks, XrayReality, AmneziaWG) |
| `backend/Tools/deploy/docker-compose.yaml` | **новый файл**, скелет с sc-tunnel сервисом |
| `backend/Tools/deploy/sc-tunnel/Dockerfile` | **новый файл**, образ с amneziawg-go + amneziawg-tools + microsocks |
| `backend/Tools/deploy/sc-tunnel/entrypoint.sh` | **новый файл**, подъём awg0 + microsocks |
| `backend/Tools/deploy/.env.example` | **новый файл** |
| `backend/Tools/deploy/README.md` | **новый файл** |
| `backend/Tools/deploy/secrets/awg_config` | **новый файл** (gitignored), AmneziaWG client config |
| `.gitignore` | добавлены паттерны `**/secrets.local.json`, `**/secrets/` |
