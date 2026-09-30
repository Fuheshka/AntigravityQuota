# Участие в разработке (Contributing to AntigravityQuota)

Спасибо за интерес к проекту **AntigravityQuota**! Любой вклад, будь то отчет об ошибке, предложение новой функции, улучшение документации или пулл-реквест с кодом, очень ценен.

---

## Как можно помочь проекту

### 1. Сообщение о багах и проблемах
Если вы столкнулись с некорректным отображением квот, вылетом приложения или странным поведением интерфейса:
1. Проверьте список [открытых Issues](https://github.com/Fuheshka/AntigravityQuota/issues), чтобы убедиться, что проблема еще не обсуждается.
2. Если ошибки нет в списке, создайте новый [Bug report](https://github.com/Fuheshka/AntigravityQuota/issues/new?template=bug_report.md).
3. Обязательно укажите:
   - Версию macOS и модель процессора (Apple Silicon / Intel).
   - Версию AntigravityQuota.
   - Шаги для воспроизведения и, если возможно, вывод консоли (`antigravity-quota --debug`) или скриншот.

### 2. Предложение новых идей и интеграций
Есть идея для нового виджета, интеграции (Raycast, Alfred, yabai, AeroSpace) или улучшения HUD?
- Откройте [Feature request](https://github.com/Fuheshka/AntigravityQuota/issues/new?template=feature_request.md).
- Опишите сценарий использования и как, по вашему мнению, это должно работать.

### 3. Тестирование на разных конфигурациях
AntigravityQuota активно тестируется на macOS 14/15 и чипах Apple Silicon. Если у вас:
- macOS 13 (Ventura)
- Процессор Intel (x86_64)
- Нестандартные конфигурации нескольких мониторов или строгих разрешений экрана
Ваша обратная связь о том, как работает приложение, исключительно полезна!

### 4. Доработка документации и локализации
- Исправление опечаток, дополнение инструкций и примеров использования.
- Улучшение скриптов интеграций (`integrations/`).
- Поддержка двуязычной документации (`README.md` на английском и `README.ru.md` на русском).

### 5. Написание кода (Pull Requests)
Мы приветствуем Pull Requests! Чтобы процесс прошел быстро и комфортно:

#### Порядок работы:
1. Сделайте **Fork** репозитория в свой GitHub-аккаунт.
2. Клонируйте свой форк и создайте отдельную ветку:
   ```bash
   git checkout -b feat/my-awesome-feature
   # или
   git checkout -b fix/correct-quota-parsing
   ```
3. Реализуйте изменения, придерживаясь принципов простоты и минимализма (YAGNI / Ponytail):
   - Не добавляйте тяжелых внешних зависимостей без крайней необходимости.
   - Используйте стандартные библиотеки Swift и системные фреймворки macOS (AppKit, Carbon, Foundation).
4. Убедитесь, что все тесты проходят:
   ```bash
   swift test
   ```
5. Проверьте сборку приложения:
   ```bash
   ./scripts/build_app.sh
   ```
6. Оформляйте коммиты на английском языке по стандарту Conventional Commits:
   - `feat: add Raycast extension script`
   - `fix: resolve HUD position glitch on multi-monitor setup`
   - `docs: update installation instructions in README`
7. Отправьте ветку в свой форк и откройте **Pull Request** в основную ветку `main` (или `dev`).

---

## Звёздочка на GitHub ⭐️

Если проект вам полезен, самый простой и приятный способ поддержать автора — поставить **звёздочку (Star)** на странице репозитория [github.com/Fuheshka/AntigravityQuota](https://github.com/Fuheshka/AntigravityQuota)!
