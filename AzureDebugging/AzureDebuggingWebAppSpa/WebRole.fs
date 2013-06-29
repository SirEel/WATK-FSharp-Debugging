namespace DebuggingWebApp

open System.ComponentModel
open System.Linq
open Microsoft.WindowsAzure.ServiceRuntime

type WebRole() =
    inherit RoleEntryPoint()

    override this.OnStart() =
        RoleEnvironment.Changing.Add(fun (e:RoleEnvironmentChangingEventArgs) ->
            if e
                .Changes
                .OfType<RoleEnvironmentConfigurationSettingChange>()
                .Any(fun change -> change.ConfigurationSettingName <> "EnableTableStorageTraceListener")
                    then e.Cancel <- true
            )
        base.OnStart()


