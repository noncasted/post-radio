# SoundCloud tunnel — план

## Цель

Дать бэкенду post-radio надёжный доступ к SoundCloud (`api-v2.soundcloud.com`,
`*.sndcdn.com`) из условий, где ISP/TSPU режут трафик. Обеспечить это и для
локальной разработки, и для прод-развёртывания через Coolify.

## Исходный замысел (пройден)

- `Audio.Socks5Proxy` в AudioOptions — единая точка включения прокси
- Общий `HttpClient` для `PlaylistLoader` + `SoundCloudClient` с
  `SocketsHttpHandler.ConnectCallback = Socks5ConnectCallback`
- Sidecar-контейнер `sc-tunnel` на VPS-тоннеле даёт SOCKS5 внутри compose-сети

## Новое решение (по результатам сессии)

**Переезд прод-хостинга с РФ на Казахстан**. KZ-хостинг не режет трафик к SC,
RF ↔ KZ тоже не блокируется. Значит никакой тоннель не нужен ни в деве
(достаточно системной AmneziaVPN когда она уже включена), ни в проде
(прямой доступ из KZ-контейнера).

## Скоуп следующей сессии

Удалить весь tunneling-код и сопутствующие артефакты, оставить только
выключатель `Audio.Socks5Proxy` на случай если KZ начнёт блочить в будущем.

## Ключевые файлы (для понимания и удаления)

| Файл | Статус для следующей сессии |
|------|----------------------------|
| `backend/Meta/Audio/AudioOptions.cs` | оставить `Socks5Proxy` как null-by-default выключатель |
| `backend/Meta/Audio/AudioServicesExtensions.cs` | упростить — убрать весь диагностический `Console.WriteLine`, логи CreateSoundCloudHandler, именованный HttpClient можно оставить но без прокси-handler если `Socks5Proxy==null` (уже так) |
| `backend/Meta/Audio/AudioServicesStartup.cs` | **убрать** try/catch + ILogger диагностику вокруг `InitializeAsync`, оставить исходный минималистичный вариант |
| `backend/Meta/Audio/Playlists/PlaylistLoader.cs` | **убрать** try/catch + `_logger.LogError` обёртку вокруг `GetTracksAsync` в `Fetch` (всё что в сессии добавлялось для диагностики) |
| `backend/Meta/Audio/Socks5ConnectCallback.cs` | **удалить файл целиком** — эта ручная SOCKS5-реализация больше не нужна |
| `backend/Orchestration/Aspire/Program.cs` | **убрать** блок про `Audio:Socks5Proxy` форварда (строки с `audioProxy`, `Console.WriteLine`, `project.WithEnvironment("Audio__Socks5Proxy", ...)`) |
| `backend/Orchestration/Aspire/appsettings.local.json` | уже чистый — `Audio` секция удалена в этой сессии |
| `backend/Tools/deploy/sc-tunnel/Dockerfile` | **удалить файл** |
| `backend/Tools/deploy/sc-tunnel/entrypoint.sh` | **удалить файл** |
| `backend/Tools/deploy/sc-tunnel/` | **удалить директорию** |
| `backend/Tools/deploy/docker-compose.yaml` | сильно упростить — `sc-tunnel` сервис выкинуть, оставить лишь скелет под будущую интеграцию остальных сервисов (или удалить весь файл, если он ещё не нужен) |
| `backend/Tools/deploy/.env.example` | удалить (нечего описывать) |
| `backend/Tools/deploy/README.md` | обновить — описать что тоннель не нужен, прод в KZ, SC доступен напрямую |
| `backend/Tools/deploy/secrets/awg_config` | удалить файл |
| `backend/Tools/DeploySetup/secrets.local.json` | убрать секции `Shadowsocks`/`XrayReality`/`AmneziaWG` — оставить только `ScTunnel` если он ещё зачем-то нужен, или удалить файл целиком |
| `.gitignore` | убрать добавленные в сессии строки `**/secrets.local.json` и `**/secrets/`, если нигде больше не применяются; или оставить как универсальный паттерн |

## Сервер (51.68.143.178)

На VPS были подняты дополнительные сервисы, которые **надо вычистить**:
- `sshd` на порту 8443 — **убрать** (`sed -i '/^Port 8443$/d' /etc/ssh/sshd_config` + reload ssh.socket)
- `shadowsocks-libev-server@post-radio.service` — **остановить и отключить**
  (`systemctl disable --now shadowsocks-libev-server@post-radio.service`);
  можно удалить конфиг `/etc/shadowsocks-libev/post-radio.json`
- `xray.service` (установлен моим скриптом в `/usr/local/etc/xray/config.json`) —
  **остановить и удалить**: `systemctl disable --now xray`; стереть
  `/usr/local/bin/xray`, `/usr/local/etc/xray/`, `/var/log/xray/`,
  `/etc/systemd/system/xray.service`, `/etc/systemd/system/xray@.service`
- Отдельный AmneziaVPN peer `post-radio-tunnel` в amnezia-client панели —
  **удалить**, оставить только личный peer
- Пара ключей `sc-tunnel-20260421` в `~ubuntu/.ssh/sc_tunnel_key{,.pub}` +
  строка в `authorized_keys` — **удалить**

## Почему такой поворот

1. Dev-test без VPN: SC в РФ у этого ISP блокируется (на уровне DPI).
2. С VPN (AmneziaVPN) SC открывается.
3. Прод в РФ → EU VPS через тоннель: перепробовали SSH, shadowsocks-libev,
   VLESS+Reality, AmneziaWG (userspace amneziawg-go) — TSPU всё режет
   (throttle после N байт, reset handshake). AmneziaWG v2 из контейнера не
   сходится handshake с сервером, хотя GUI-клиент на той же машине работает.
4. Вывод: для прода разумнее сменить юрисдикцию на KZ, где этой проблемы нет.
