using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Layout;
using DevExpress.ExpressApp.Updating;
using Fasterflect;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.ModelAdapter;

namespace Xpand.ExpressApp.WorldCreator.System {
    public class WorldCreatorApplication : XafApplication, ITestXafApplication {
        private static readonly object Locker = new object();

        public WorldCreatorApplication(IObjectSpaceProvider objectSpaceProvider, IEnumerable<ModuleBase> moduleList) {
            objectSpaceProviders.Add(objectSpaceProvider);
            var moduleBases = moduleList.Select(m => m.GetType().CreateInstance()).Cast<ModuleBase>().OrderBy(m => m.Name).Distinct().ToArray();
            foreach (var moduleBase in moduleBases) {
                if (Modules.FindModule(moduleBase.GetType()) == null)
                    Modules.Add(moduleBase);
            }
        }

        protected override void OnDatabaseVersionMismatch(DatabaseVersionMismatchEventArgs e){
            e.Updater.Update();
            e.Handled = true;
        }

        internal static void CheckCompatibility(XafApplication application,Func<IObjectSpaceProvider, ModuleList, WorldCreatorApplication> func) {
            lock (Locker) {
                var objectSpaceProvider = WorldCreatorObjectSpaceProvider.Create(application, false);
                using (var worldCreatorApplication = func(objectSpaceProvider, application.Modules)) {
                    worldCreatorApplication.ApplicationName = application.ApplicationName;
                    try {
                        worldCreatorApplication.CheckCompatibility();
                    }
                    catch (CompatibilityException e) {
                        if (e.Message.Contains("FK_TemplateInfo_ObjectType")) {
                            var message = "Please use " + typeof(WorldCreatorTypeInfoSource).Name + "." +
                                          nameof(WorldCreatorTypeInfoSource.UseDefaultObjectTypePersistance) +
                                          " before " + application.GetType().Name;
                            throw new CompatibilityException(new CompatibilityError(message, e));
                        }
                        throw;
                    }
                    using (objectSpaceProvider.CreateUpdatingObjectSpace(Debugger.IsAttached || InterfaceBuilder.IsDBUpdater)) {

                    }
                }
                objectSpaceProvider.ResetThreadSafe();
            }
        }

        public override DatabaseUpdaterBase CreateDatabaseUpdater(IObjectSpaceProvider objectSpaceProvider) {
            var databaseUpdaterBase = base.CreateDatabaseUpdater(objectSpaceProvider);
            var databaseSchemaUpdater = databaseUpdaterBase as DatabaseSchemaUpdater;
            if (databaseSchemaUpdater != null)
                return new WorldCreatorSchemaDatabaseUpdater(objectSpaceProvider, Modules);
            return new WorldCreatorDatabaseUpdater(objectSpaceProvider, ApplicationName, Modules);
        }

        private class WorldCreatorDatabaseUpdater : DatabaseUpdater {
            public WorldCreatorDatabaseUpdater(IObjectSpaceProvider objectSpaceProvider, string applicationName, ModuleList moduleBases)
                : base(objectSpaceProvider, moduleBases, applicationName, objectSpaceProvider.ModuleInfoType) {
            }

            protected override IList<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, IList<IModuleInfo> versionInfoList) {
                var moduleUpdaters = base.GetModuleUpdaters(objectSpace, versionInfoList);
                List<ModuleUpdater> dbUpdaters = new List<ModuleUpdater>();
                var moduleUpdaterTypes = XafTypesInfo.Instance.FindTypeInfo(typeof(WorldCreatorModuleUpdater))
                    .Descendants.Where(info => !info.IsAbstract).ToArray();
                foreach (ModuleBase module in modules) {
                    Version moduleVersionFromDB = GetModuleVersion(versionInfoList, module.Name);
                    if (ForceUpdateDatabase || module.Version > moduleVersionFromDB || UseAllModuleUpdaters) {
                        dbUpdaters.AddRange(GetModuleUpdaters(module, moduleUpdaterTypes, objectSpace, moduleVersionFromDB));
                    }
                }
                return dbUpdaters;


            }

            private IEnumerable<ModuleUpdater> GetModuleUpdaters(ModuleBase module, ITypeInfo[] moduleUpdaterTypes, IObjectSpace objectSpace, Version moduleVersionFromDB) {
                var typeInfos = moduleUpdaterTypes.Where(info => info.Type.Assembly == module.GetType().Assembly);
                return typeInfos.Select(info => info.Type.CreateInstance(objectSpace, moduleVersionFromDB)).Cast<ModuleUpdater>();
            }
        }

        private class WorldCreatorSchemaDatabaseUpdater : DatabaseSchemaUpdater {
            public WorldCreatorSchemaDatabaseUpdater(IObjectSpaceProvider objectSpaceProvider, ModuleList moduleList) : base(objectSpaceProvider, moduleList) {

            }
        }

        protected override LayoutManager CreateLayoutManagerCore(bool simple) {
            return null;
        }
    }
}