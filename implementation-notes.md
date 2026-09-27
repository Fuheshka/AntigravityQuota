# Implementation notes: AntigravityQuota

## 1. Выявленный механизм работы лимитов в Antigravity 2.x

- При запуске `Antigravity.app` поднимается локальный фоновый процесс `/Applications/Antigravity.app/Contents/Resources/bin/language_server` с аргументом `--csrf_token <uuid>`.
- Сервер открывает два локальных TCP-порта на `127.0.0.1` (один HTTPS с самоподписанным сертификатом и один чистый HTTP, обычно с номером на 1 больше).
- Встроенный UI Antigravity кэширует состояние квот при старте и не обновляет выпадающее меню моделей (`View Usage`), пока пользователь вручную не нажмет кнопку обновления в `Settings -> Models`.
- При нажатии кнопки обновления вызывается Connect-RPC метод:
  `POST /exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary` с телом `{"forceRefresh": true}` и заголовками `Connect-Protocol-Version: 1` и `x-codeium-csrf-token: <token>`.
- Этот метод возвращает живую сводку по обоим пулам (`Gemini Models` и `Claude and GPT models`) в двух разрезах:
  - **5-часовой скользящий лимит (`window: "5h"`)**: точная доля остатка `remainingFraction` и UTC-время полного сброса `resetTime`.
  - **Недельный лимит (`window: "weekly"`)**: доля остатка и точное время сброса.
- Дополнительно метод `GetCascadeModelConfigData` возвращает список всех конкретных моделей (`Gemini 3.8 Flash (High)`, `Claude Opus 4.6 (Thinking)` и др.) с привязкой к их квоте.

## 2. Архитектурные решения и компромиссы (Ponytail Mode)

- **Отказ от модификации `app.asar`**: В `Antigravity.app` включена проверка SHA-256 целостности Electron ASAR и подписи Gatekeeper. Любой патч внутри бандла ломал бы подпись и слетал при каждом автообновлении. Внешний нативный демон (`LSUIElement = true`) работает полностью независимо и не требует вмешательства в файлы `Antigravity.app`.
- **Отсутствие прав Accessibility (TCC)**: Для отслеживания активности окна Antigravity вместо `CGEventTap` или `AXUIElement` используется системный центр уведомлений `NSWorkspace.didActivateApplicationNotification` и `NSWorkspace.shared.frontmostApplication`. Это избавляет пользователя от выдачи спецправ в «Конфиденциальность и безопасность».
- **Два режима плавающего HUD-виджета**:
  - *Развернутая карточка*: показывает прогресс-бары 5-часового лимита, проценты, таймер обратного отсчета и недельный остаток по обоим пулам.
  - *Компактная таблетка (Pill Mode)*: свернутый вид `● G 85.4% 1ч 20м · ● C 100%` для минимального занятия места поверх интерфейса Antigravity.
- **Устойчивость к перезапуску**: Если `Antigravity.app` был перезапущен и `language_server` сменил PID, порт или CSRF-токен, `QuotaClient` при первой ошибке соединения автоматически пересканирует `ps` и `lsof` и прозрачно переподключается.
