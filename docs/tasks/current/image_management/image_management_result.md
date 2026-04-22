## image_management — Результат

### Статус: Завершено

### Что сделано
- Добавлена кнопка `Paste`: читает clipboard, парсит ссылку на `.jpg/.jpeg/.png`, скачивает изображение сервером и показывает success/error toast.
- Страница Console `/images` превращена в менеджер картинок: count в заголовке, actions, пустое состояние, compact grid preview 4x3 и pagination по 12 элементов без скролла страницы.
- Добавлено отдельное upload-окно в стилистике Console cards с drag-and-drop зоной, выбором нескольких файлов и выбором папки через browser file picker.
- Добавлен полноэкранный просмотр картинки на затемненном фоне с закрытием по фону/крестику, download и delete.
- Расширено файловое media storage: metadata list, безопасное сохранение изображений с уникальными именами, delete, zip archive stream.
- `ImagesCollection` теперь хранит metadata, умеет save/delete и сохраняет совместимость с frontend count/index URL API.
- ConsoleGateway получил защищенные endpoints для inline image, single download и all-images zip download.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Common/Storage/MediaStorage.cs` | Добавлены `MediaImage`, list/save/delete/archive для изображений |
| `backend/Meta/Images/ImagesCollection.cs` | Добавлены metadata, save/delete, local refresh + queue refresh |
| `backend/Console/Radio/Images.razor` | Реализован полный UI менеджмента картинок |
| `backend/Console/Radio/ImageGridItem.razor` | Новый компонент preview-карточки для grid |
| `backend/Orchestration/ConsoleGateway/ConsoleMediaEndpoints.cs` | Новые endpoints для preview/download/archive |
| `backend/Orchestration/ConsoleGateway/Program.cs` | Подключены console media endpoints |
| `backend/Orchestration/ConsoleGateway/App.razor` | Добавлен JS helper `downloadFileFromUrl` |

### Проверка
- `dotnet build backend/post-radio.slnx` — успешно, 0 warnings, 0 errors. Повторно прогнано после restyle upload modal.
- `dotnet test backend/post-radio.slnx --no-build` — exit code 0, тестовых проектов/вывода нет.
- `git diff --check` — без замечаний.
- Локальный `/check` checklist: Blazor inject/early-return/component extraction, lifetime, public API, state/transaction/race relevance — без найденных блокеров.

### Нерешенные вопросы
- Настоящие thumbnail-файлы не генерируются: preview реализован ленивой загрузкой `<img loading="lazy">` через console endpoint, чтобы не добавлять image-processing dependency.
