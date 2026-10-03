# Tomatotodo interface localization

Targets: Android 1.0.3 and native Windows 1.6.2.

## Contract

- General / Common starts with Language; default is the primary system locale.
- There are 41 language, region and script options. Unsupported primary languages fall back to US English.
- Interface preference belongs to the device and is excluded from user JSON, import/export and cloud sync.
- Only application-owned interface copy is translated. User names, tasks, courses, profiles and cloud announcements retain their original text.
- Arabic and Hebrew use RTL. Serbian Latin is transliterated independently; English, Portuguese and Spanish regional variants have separate catalogs.
- Resources are bundled offline. The running applications make no translation-service calls.

## Resources and checks

`source.json` contains source-owned message keys. Numeric placeholders such as `{0}` retain their identity across translations; format arguments remain separate, preserving user content and values.

`translate_batch.py` generates checkpointed **machine translation drafts** using independent entries. `core-terms.json` and `finalize.py` supply terminology corrections and regional normalization. Run `python localization/finalize.py` to reject missing messages, empty translations and placeholder errors, then embed identical catalogs in Flutter assets, Android native assets and the Windows assembly.

`review-status.json` explicitly records required linguistic review. Automatic checks establish resource integrity, not human-reviewed linguistic accuracy. Regional normalization also requires native-language review before a release can claim that status.

Do not rerun the source extraction scripts on already transformed application files: they are one-time migration helpers, not idempotent code generators. Add new keys to the registry and translate them without changing persistence schemas.

Flutter: run `flutter test --no-pub test/app_language_test.dart test/localization_ui_test.dart`; Windows: run `dotnet run --project tests/Language.Tests.csproj`. The Flutter suite covers native localization delegates, RTL, every key and placeholder, and changing language while a settings route remains open.
