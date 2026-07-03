// Estado global compartilhado (SettingsService.SettingsPath, Console, L.Lang,
// SecretProtector.Provider) -> suite roda sequencial por seguranca.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
