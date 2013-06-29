namespace FsWeb

open System
open System.Diagnostics
open System.Linq
open System.Web
open System.Web.Mvc
open System.Web.Routing
open System.Web.Http
open System.Web.Optimization
open System.IO

open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.ServiceRuntime

type BundleConfig() =
    static member RegisterBundles (bundles:BundleCollection) =
        bundles.Add(ScriptBundle("~/bundles/jquery").Include("~/Scripts/jquery-1.*"))

        bundles.Add(ScriptBundle("~/bundles/jqueryui").Include("~/Scripts/jquery-ui*"))

        bundles.Add(ScriptBundle("~/bundles/jqueryval").Include(
                                     "~/Scripts/jquery.unobtrusive*",
                                     "~/Scripts/jquery.validate*"))
            
        bundles.Add(ScriptBundle("~/bundles/extLibs").Include(
                                     "~/Scripts/underscore.js",   
                                     "~/Scripts/backbone.js",
                                     "~/Scripts/toastr.js",
                                     "~/Scripts/knockout-2.1.0.js"))

        if Directory.Exists(HttpContext.Current.Server.MapPath "/Scripts/models") then                                     
            bundles.Add(ScriptBundle("~/bundles/localApp").Include(
                                     "~/Scripts/app/main.js",
                                     "~/Scripts/app/utility.js",
                                     "~/Scripts/models/*.js",
                                     "~/Scripts/views/*.js",
                                     "~/Scripts/app/router.js"))
        else                                     
            bundles.Add(ScriptBundle("~/bundles/localApp").Include(
                                     "~/Scripts/app/main.js",
                                     "~/Scripts/app/utility.js",
                                     "~/Scripts/viewModels/*.js",
                                     "~/Scripts/app/router.js"))

        bundles.Add(ScriptBundle("~/bundles/sammy").IncludeDirectory("~/Scripts/Sammy", "*.js", true))

        bundles.Add(ScriptBundle("~/bundles/modernizr").Include("~/Scripts/modernizr-*"))

        bundles.Add(StyleBundle("~/Content/css").Include("~/Content/*.css"))

        bundles.Add(StyleBundle("~/Content/themes/base/css").Include(
                                    "~/Content/themes/base/jquery.ui.core.css",
                                    "~/Content/themes/base/jquery.ui.resizable.css",
                                    "~/Content/themes/base/jquery.ui.selectable.css",
                                    "~/Content/themes/base/jquery.ui.accordion.css",
                                    "~/Content/themes/base/jquery.ui.autocomplete.css",
                                    "~/Content/themes/base/jquery.ui.button.css",
                                    "~/Content/themes/base/jquery.ui.dialog.css",
                                    "~/Content/themes/base/jquery.ui.slider.css",
                                    "~/Content/themes/base/jquery.ui.tabs.css",
                                    "~/Content/themes/base/jquery.ui.datepicker.css",
                                    "~/Content/themes/base/jquery.ui.progressbar.css",
                                    "~/Content/themes/base/jquery.ui.theme.css"))

type Route = { controller : string; action : string; id : UrlParameter }
type ApiRoute = { id : RouteParameter }

type AzureExceptionFilterAttribute() =
    inherit System.Web.Http.Filters.ExceptionFilterAttribute()
    override this.OnException(context) =
        Trace.TraceError(context.Exception.Message)

type Global() =
    inherit System.Web.HttpApplication() 

    static member RegisterGlobalFilters (filters:GlobalFilterCollection) =
        filters.Add(new HandleErrorAttribute())

    static member RegisterRoutes(routes:RouteCollection) =
        routes.IgnoreRoute( "{resource}.axd/{*pathInfo}" )
        routes.MapHttpRoute( "DefaultApi", "api/{controller}/{id}", 
            { id = RouteParameter.Optional } ) |> ignore
        routes.MapRoute("Default", "{controller}/{action}/{id}", 
            { controller = "Home"; action = "Index"; id = UrlParameter.Optional } ) |> ignore

    member this.Start() =

        CloudStorageAccount.SetConfigurationSettingPublisher
            (
                Action<_,_>(fun (configName:string) (configSetter:Func<_,_>) ->
                    configName
                    |> RoleEnvironment.GetConfigurationSettingValue
                    |> configSetter.Invoke 
                    |> ignore
                )
            )

        let configureTraceListener = function
            | true  ->
                let enableTraceListenerSetting = 
                    RoleEnvironment.GetConfigurationSettingValue("EnableTableStorageTraceListener")
                match bool.TryParse(enableTraceListenerSetting) with
                |true, true    ->
                    let listener = 
                        new AzureDiagnostics.TableStorageTraceListener
                            (
                                "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"
                                , Name = "TableStorageTraceListener"
                            )
                    Trace.Listeners.Add(listener) |> ignore
                    Trace.AutoFlush <- true
                |true, false   ->
                    Trace.Listeners.Remove("TableStorageTraceListener")
                |false, _      -> ()
            | _ -> ()

        configureTraceListener true

        RoleEnvironment.Changed.Add(fun e ->
            e
                .Changes
                .OfType<RoleEnvironmentConfigurationSettingChange>()
                .Any(fun change -> change.ConfigurationSettingName = "EnableTableStorageTraceListener")
                |> configureTraceListener
            )

        GlobalConfiguration
            .Configuration
            .Filters
            .Add(new AzureExceptionFilterAttribute())

        AreaRegistration.RegisterAllAreas()
        Global.RegisterRoutes RouteTable.Routes
        Global.RegisterGlobalFilters GlobalFilters.Filters
        BundleConfig.RegisterBundles BundleTable.Bundles

    member this.Error() =
        base.Server
            .GetLastError()
            .Message
            |> Trace.TraceError

