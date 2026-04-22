## Задача: менеджмент картинок

### Цель
Сделать полноценный менеджмент картинок на странице Console `/images`: отображение всех загруженных файлов, грид 4x4 с пагинацией, загрузка файлов/папок через отдельное окно, скачивание архивом, счетчик в шапке и полноэкранный просмотр с действиями скачать/удалить.

### Контекст
Сейчас страница `Images.razor` показывает только количество файлов и кнопку refresh. Хранилище картинок уже есть в `MediaStorage`, а `ImagesCollection` используется frontend-слайдшоу через count/index URL. Нужно расширить существующие сервисы без новой зависимости и не сломать public radio endpoints.

### Шаги реализации

**1. Показать все картинки и счетчик**
  1.1. Расширить модель картинок в storage — `backend/Common/Storage/MediaStorage.cs`
  1.2. Отдать список metadata из image collection — `backend/Meta/Images/ImagesCollection.cs`
  1.3. Переработать страницу `/images` с count в заголовке — `backend/Console/Radio/Images.razor`

**2. Грид 4x4 и пагинация с preview**
  2.1. Создать компонент карточки картинки — `backend/Console/Radio/ImageGridItem.razor` [новый файл]
  2.2. Сделать page size 16 и ленивые `<img loading="lazy">` preview — `backend/Console/Radio/Images.razor`

**3. Скачать все архивом**
  3.1. Добавить запись zip-архива в storage — `backend/Common/Storage/MediaStorage.cs`
  3.2. Добавить endpoint архива в ConsoleGateway — `backend/Orchestration/ConsoleGateway/ConsoleMediaEndpoints.cs` [новый файл]
  3.3. Подключить endpoint в startup — `backend/Orchestration/ConsoleGateway/Program.cs`

**4. Загрузка через отдельное окно**
  4.1. Добавить upload modal с drag-and-drop зоной — `backend/Console/Radio/Images.razor`
  4.2. Добавить multi-file и folder picker через `InputFile` — `backend/Console/Radio/Images.razor`
  4.3. Сохранять изображения через storage/collection и refresh — `backend/Common/Storage/MediaStorage.cs`, `backend/Meta/Images/ImagesCollection.cs`

**5. Полноэкранный просмотр, скачать и удалить**
  5.1. Добавить console endpoints inline/single download — `backend/Orchestration/ConsoleGateway/ConsoleMediaEndpoints.cs` [новый файл]
  5.2. Добавить затемненный overlay и закрытие по фону/крестику — `backend/Console/Radio/Images.razor`
  5.3. Добавить download/delete actions — `backend/Console/Radio/Images.razor`
  5.4. Добавить JS helper для скачивания URL — `backend/Orchestration/ConsoleGateway/App.razor`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Console/Radio/Images.razor` | Основная UI-страница менеджмента картинок |
| `backend/Console/Radio/ImageGridItem.razor` | Карточка preview в гриде |
| `backend/Common/Storage/MediaStorage.cs` | Файловое хранение, upload/delete/list/archive |
| `backend/Meta/Images/ImagesCollection.cs` | Синхронизация списка и совместимость frontend count/index API |
| `backend/Orchestration/ConsoleGateway/ConsoleMediaEndpoints.cs` | Защищенная отдача картинок и zip в console host |
| `backend/Orchestration/ConsoleGateway/Program.cs` | Регистрация console media endpoints |
| `backend/Orchestration/ConsoleGateway/App.razor` | JS helper для download URL |

### Документация к прочтению
- `.codex/docs/BLAZOR.md` — правила Razor UI, inject, extracted component for collections.
- `.codex/docs/CODE_STYLE_FULL.md` — стиль C# и порядок членов.
- `.codex/docs/COMMON_ORLEANS.md` — durable queue refresh в `ImagesCollection`.
- `.codex/docs/COMMON_LIFETIMES.md` — lifetime для queue listener.

### Риски
- Настоящие миниатюры не генерируются без image-processing зависимости; preview реализуется ленивой загрузкой через browser `<img>`.
- Скачивание большого архива может быть тяжелым по I/O, поэтому endpoint должен писать zip потоком.
