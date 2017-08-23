using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Validation;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Validation;
using DevExpress.Utils;
using DevExpress.Xpo;
using Xpand.ExpressApp.ModelDifference.Controllers;
using Xpand.ExpressApp.ModelDifference.DataStore.BaseObjects;
using Xpand.ExpressApp.ModelDifference.DataStore.Validation;
using Xpand.ExpressApp.ModelDifference.NodeUpdaters;
using Xpand.ExpressApp.ModelDifference.Security.Improved;
using Xpand.ExpressApp.Security.Permissions;
using Xpand.Persistent.Base;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.Security;


namespace Xpand.ExpressApp.ModelDifference {
    public interface IModelOptionsModelDifference:IModelNode{
        [Category(ModelDifferenceModule.ModelDifferenceCategory)]
        [DefaultValue("Autocreated at {0} For {1}")]
        string UserModelDifferenceObjectSubjectTemplate { get; set; }
        [Category(ModelDifferenceModule.ModelDifferenceCategory)]
        [Editor("DevExpress.ExpressApp.Win.Core.ModelEditor.CriteriaModelEditorControl, DevExpress.ExpressApp.Win" + XafAssemblyInfo.VersionSuffix + XafAssemblyInfo.AssemblyNamePostfix, typeof(UITypeEditor))]
        [CriteriaOptions("MDOTypeInfo")]
        string ModelToUpdateFromFileCriteria { get; set; }
        [Browsable(false)]
        ITypeInfo  MDOTypeInfo { get; }
    }

    [DomainLogic(typeof(IModelOptionsModelDifference))]
    public class ModelOptionsModelDifferenceLogic {
        public static ITypeInfo Get_MDOTypeInfo(IModelOptionsModelDifference modelDifference){
            return modelDifference.Application.BOModel.GetClass(typeof(ModelDifferenceObject)).TypeInfo;
        }

        public static string Get_ModelToUpdateFromFileCriteria(IModelOptionsModelDifference modelDifference) {
            return new XPQuery<ModelDifferenceObject>(XpoTypesInfoHelper.GetXpoTypeInfoSource().XPDictionary).TransformExpression(
                    o =>o.DifferenceType == DifferenceType.Model && o.Name.Contains(modelDifference.Application.Title) && !o.Disabled).ToString();
        }

    }
    [ToolboxItem(true)]
    [ToolboxTabName(XpandAssemblyInfo.TabWinWebModules)]
    public sealed class ModelDifferenceModule : XpandModuleBase, ISequenceGeneratorUser,ISecurityModuleUser{
        public const string ModelDifferenceCategory = "eXpand.ModelDifference";
        public ModelDifferenceModule() {
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.CloneObject.CloneObjectModule));
            RequiredModuleTypes.Add(typeof(ExpressApp.Security.XpandSecurityModule));
            RequiredModuleTypes.Add(typeof(ValidationModule));
        }

        public override void ExtendModelInterfaces(ModelInterfaceExtenders extenders){
            base.ExtendModelInterfaces(extenders);
            extenders.Add<IModelOptions, IModelOptionsModelDifference>();
        }

        protected override IEnumerable<Type> GetDeclaredControllerTypes(){
            var types = new[]{
                typeof(CloneObjectViewController), typeof(CombineActiveUserDiffsWithLastLayerController),
                typeof(MergeDifferencesController),typeof(ModelDifferenceObjectsRuntimeMembersController),typeof(ReloadApplicationModelController)
            };
            return GetDeclaredControllerTypesCore(types);
        }

        public override void CustomizeTypesInfo(ITypesInfo typesInfo) {
            base.CustomizeTypesInfo(typesInfo);
            if (!RuntimeMode) {
                CreateWeaklyTypedCollection(typesInfo, typeof(UserModelDifferenceObject), "Users");
                CreateWeaklyTypedCollection(typesInfo, typeof(RoleModelDifferenceObject), "Roles");
            } else if (Application.CanBuildSecurityObjects()) {
                BuildSecuritySystemObjects();
            }
        }

        public override void Setup(ApplicationModulesManager moduleManager){
            base.Setup(moduleManager);
            this.AddSecurityObjectsToAdditionalExportedTypes("Xpand.Persistent.BaseImpl.ModelDifference");
            ValidationRulesRegistrator.RegisterRule(moduleManager, typeof(XmlContentCodeRule), typeof(IRuleBaseProperties));
        }

        public override void Setup(XafApplication application) {
            base.Setup(application);
            if (application != null && !DesignMode) {
                application.SettingUp += ApplicationOnSettingUp;
                application.SetupComplete+=ApplicationOnSetupComplete;
            }
            AddToAdditionalExportedTypes(typeof(ModelDifferenceObject).Namespace, GetType().Assembly);
        }

        private void ApplicationOnSetupComplete(object sender, EventArgs eventArgs){
            var securityStrategy = ((XafApplication)sender).Security as SecurityStrategy;
            if (securityStrategy != null) {
                (securityStrategy).CustomizeRequestProcessors += OnCustomizeRequestProcessors;
            }
        }

        void ApplicationOnSettingUp(object sender, EventArgs eventArgs) {
            BuildSecuritySystemObjects();
        }

        void BuildSecuritySystemObjects() {
            var dynamicSecuritySystemObjects = new DynamicSecuritySystemObjects(Application);
            var xpMemberInfos = dynamicSecuritySystemObjects.BuildUser(typeof(UserModelDifferenceObject), "UserUsers_UserModelDifferenceObjectUserModelDifferenceObjects", "UserModelDifferenceObjects", "Users");
            dynamicSecuritySystemObjects.HideInDetailView(xpMemberInfos, "UserModelDifferenceObjects");
            xpMemberInfos = dynamicSecuritySystemObjects.BuildRole(typeof(RoleModelDifferenceObject), "RoleRoles_RoleModelDifferenceObjectRoleModelDifferenceObjects", "RoleModelDifferenceObjects", "Roles");
            dynamicSecuritySystemObjects.HideInDetailView(xpMemberInfos, "RoleModelDifferenceObjects");
        }

        void OnCustomizeRequestProcessors(object sender, CustomizeRequestProcessorsEventArgs e) {
            var keyValuePair = new KeyValuePair<Type, IPermissionRequestProcessor>(typeof(ModelCombinePermissionRequest), e.Permissions.GetProcessor<ModelCombineRequestProcessor>());
            e.Processors.Add(keyValuePair);
        }
        public override void AddGeneratorUpdaters(ModelNodesGeneratorUpdaters updaters) {
            base.AddGeneratorUpdaters(updaters);
            updaters.Add(new BOModelNodesUpdater());
            updaters.Add(new BOModelMemberNodesUpdater());
        }
    }
}