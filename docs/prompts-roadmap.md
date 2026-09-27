# Каталог промптов Vibe-Coding Pipeline для AntigravityQuota

> [!abstract] Интерактивный сборник промптов (кликабельные чекбоксы и сворачиваемые блоки)
> Здесь расписаны задачи до релиза `v1.0.0`: **одна маленькая задача = один короткий промпт**. Скопируй нужный промпт целиком и отправь в чат в папке `/Users/fuheshka/Documents/GitHub/AntigravityQuota`.
> 
> - **Кликабельные чекбоксы:** все промпты оформлены через интерактивные списки `- [x]` и `- [ ]`.
> - **Сворачивание без сброса:** все активные промпты открыты по умолчанию (`> [!note]+`), но отделены пустой строкой от чекбокса в независимые секции Obsidian.
> - **Интерактивные слэш-команды:** каждый промпт начинается с кликабельных слэш-команд в формате `[/command](slashCommand;command)`.

---

## Этап 1: Упаковка, иконка и релиз v1.0.0 (Промпты 1.1 - 1.3)

- [x] **Промпт 1.1: Генерация и упаковка фирменной иконки macOS (AppIcon.icns)**

> [!note]- Текст промпта 1.1
> ```text
> [/goal](slashCommand;goal) [/using-superpowers](slashCommand;using-superpowers) [/vibe-coding](slashCommand;vibe-coding) [/apple-design](slashCommand;apple-design) [/better-ui](slashCommand;better-ui) [/ponytail](slashCommand;ponytail)
>
> ### 1. Цель
> Сгенерировать, согласовать и упаковать фирменную иконку `AppIcon.icns` для нативной утилиты **AntigravityQuota** в `/Users/fuheshka/Documents/GitHub/AntigravityQuota`.
>
> ### 2. Критерии приемки (Definition of Done)
> - Через `generate_image` сгенерирован концепт иконки в стиле Apple HIG (macOS Sequoia squircle, глубокий темно-индиговый/графитовый фон, неоновая шкала-спидометр квоты с изумрудным и голубым свечением, без текста и лишнего шума).
> - Изображение показано мне в чате и получено прямое подтверждение перед сборкой `.icns`.
> - Создан скрипт `scripts/generate_icns.sh` (на базе нативных `sips` и `iconutil`), который собирает `Resources/AppIcon.icns` со всеми разрешениями (`16x16..1024x1024` `@1x/@2x`).
> - В `scripts/build_app.sh` и `Info.plist` подключен `AppIcon.icns`, пересобран бандл `/Applications/AntigravityQuota.app`.
>
> ### 3. Границы (Scope Boundaries)
> - **Что трогаем**: `Resources/AppIcon.icns`, `scripts/generate_icns.sh`, `scripts/build_app.sh`.
> - **Чего НЕ трогаем**: сетевой слой `QuotaClient.swift` и логику HUD-панели.
>
> ### 4. Проверка
> - Проверь сборку `.app`, запиши решение в `implementation-notes.md` и обнови `/Users/fuheshka/Documents/Obsidian Vault/.agents/MEMORY.md`. Не делай git commit без моего подтверждения.
> ```

- [ ] **Промпт 1.2: Сборка брендированного установщика .dmg и архива .zip**

> [!note]+ Текст промпта 1.2
> ```text
> [/goal](slashCommand;goal) [/using-superpowers](slashCommand;using-superpowers) [/vibe-coding](slashCommand;vibe-coding) [/macos-dmg-designer](slashCommand;macos-dmg-designer) [/macos-native-utility](slashCommand;macos-native-utility) [/ponytail](slashCommand;ponytail)
>
> ### 1. Цель
> Создать пайплайн упаковки релиза и собрать брендированный установщик `.dmg` и архив `.zip` для **AntigravityQuota** в `/Users/fuheshka/Documents/GitHub/AntigravityQuota`.
>
> ### 2. Критерии приемки (Definition of Done)
> - По стандарту навыка `macos-dmg-designer` создан скрипт `scripts/package_release.sh`.
> - Сгенерирован Retina-фон `dmg_background.tiff` (`660x440`), настроено окно `AntigravityQuota.app -> Applications` с контрастными карточками подписей (`y=208..242` при иконках `128px` на `y=140`), скрыты системные файлы на `y=500`, зафиксированы границы окна Finder без скроллбаров.
> - Собраны готовые дистрибутивы `dist/AntigravityQuota-v1.0.0-macOS.dmg` и `dist/AntigravityQuota-v1.0.0-macOS.zip`.
>
> ### 3. Границы (Scope Boundaries)
> - **Что трогаем**: `scripts/package_release.sh`, ассеты фона DMG и папку `dist/`.
> - **Чего НЕ трогаем**: исходный Swift-код в `Sources/`.
>
> ### 4. Проверка
> - Проверь монтирование `.dmg` и отсутствие обрезки подписей в окне Finder, запиши решение в `implementation-notes.md` и обнови `/Users/fuheshka/Documents/Obsidian Vault/.agents/MEMORY.md`. Не делай git commit без моего подтверждения.
> ```

- [ ] **Промпт 1.3: Оформление и публикация релиза v1.0.0 на GitHub Releases**

> [!note]+ Текст промпта 1.3
> ```text
> [/goal](slashCommand;goal) [/using-superpowers](slashCommand;using-superpowers) [/vibe-coding](slashCommand;vibe-coding) [/github-release-manager](slashCommand;github-release-manager) [/sepia](slashCommand;sepia) [/verification-before-completion](slashCommand;verification-before-completion)
>
> ### 1. Цель
> Подготовить описание в стиле Flowseal / Confeden и опубликовать официальный релиз `v1.0.0` утилиты **AntigravityQuota** на GitHub с прикрепленными `.dmg` и `.zip` пакетами.
>
> ### 2. Критерии приемки (Definition of Done)
> - Проверено прохождение всех unit-тестов (`swift test`) и наличие файлов `dist/AntigravityQuota-v1.0.0-macOS.dmg` и `dist/AntigravityQuota-v1.0.0-macOS.zip`.
> - По стандарту `github-release-manager` подготовлен лаконичный двуязычный (RU/EN) черновик release notes с таблицей контрольных сумм `SHA-256` без воды и машинных штампов.
> - Черновик показан мне в чате и получено прямое подтверждение перед вызовом `gh release create v1.0.0`.
>
> ### 3. Границы (Scope Boundaries)
> - **Что трогаем**: GitHub Release `v1.0.0` и документацию релиза.
> - **Чего НЕ трогаем**: исходники и тесты приложения.
>
> ### 4. Проверка
> - Убедись, что релиз опубликован и оба бинарных архива скачиваются по ссылкам, обнови `/Users/fuheshka/Documents/Obsidian Vault/.agents/MEMORY.md`.
> ```

