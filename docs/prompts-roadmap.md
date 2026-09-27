# Каталог промптов для следующих этапов AntigravityQuota

Скопируй и отправь нужный промпт в чат проекта `AntigravityQuota`, чтобы выполнить соответствующий этап.

---

## Этап 1: Фирменная иконка приложения (`AppIcon.icns`)

[/using-superpowers](slashCommand;using-superpowers) [/apple-design](slashCommand;apple-design) [/better-ui](slashCommand;better-ui) [/ponytail](slashCommand;ponytail)

Работаем в `/Users/fuheshka/Documents/GitHub/AntigravityQuota`.
1. Сгенерируй через `generate_image` концепт фирменной иконки macOS (`AppIcon`) для утилиты **AntigravityQuota** в стиле Apple HIG (macOS Sequoia squircle, глубокий темно-индиговый/графитовый фон, неоновая шкала-спидометр квоты с изумрудным и голубым свечением, без текста и лишнего шума).
2. Обязательно покажи сгенерированное изображение и дождись моего подтверждения (или правок).
3. После согласования напиши скрипт `scripts/generate_icns.sh` (на базе нативных `sips` и `iconutil`), собери полноразмерный `Resources/AppIcon.icns` (16x16..1024x1024 @1x/@2x), подключи его в `scripts/build_app.sh` и пересобери `/Applications/AntigravityQuota.app`.

---

## Этап 2: Сборка установщика `.dmg` и `.zip` дистрибутива

[/using-superpowers](slashCommand;using-superpowers) [/macos-dmg-designer](slashCommand;macos-dmg-designer) [/macos-native-utility](slashCommand;macos-native-utility) [/ponytail](slashCommand;ponytail) [/verification-before-completion](slashCommand;verification-before-completion)

Работаем в `/Users/fuheshka/Documents/GitHub/AntigravityQuota`.
1. По правилам навыка `macos-dmg-designer` создай пайплайн упаковки релиза `scripts/package_release.sh`.
2. Сгенерируй Retina-фон `dmg_background.tiff` (`660x440`), настрой перетаскивание `AntigravityQuota.app -> Applications` с контрастными карточками подписей (`y=208..242` при иконках `128px` на `y=140`), скрой служебные файлы на `y=500` и жестко зафиксируй размер окна Finder без скроллбаров.
3. Собери готовые артефакты `dist/AntigravityQuota-v1.0.0-macOS.dmg` и `dist/AntigravityQuota-v1.0.0-macOS.zip`, проверь монтирование DMG и отображение всех ярлыков.

---

## Этап 3: Публикация релиза `v1.0.0` на GitHub

[/using-superpowers](slashCommand;using-superpowers) [/github-release-manager](slashCommand;github-release-manager) [/sepia](slashCommand;sepia) [/ponytail](slashCommand;ponytail) [/verification-before-completion](slashCommand;verification-before-completion)

Работаем в `/Users/fuheshka/Documents/GitHub/AntigravityQuota`.
1. Проверь чистоту тестов (`swift test`) и наличие собранных артефактов `AntigravityQuota-v1.0.0-macOS.dmg` и `AntigravityQuota-v1.0.0-macOS.zip`.
2. По стандарту `github-release-manager` (стиль Flowseal / Confeden: лаконично, двуязычно RU/EN, без воды и машинных штампов) подготовь черновик описания релиза `v1.0.0` с таблицей контрольных сумм SHA-256.
3. Покажи мне финальный текст релиза на согласование, и после моего подтверждения опубликуй тег `v1.0.0` и релиз через `gh release create` с прикрепленными `.dmg` и `.zip` файлами.
