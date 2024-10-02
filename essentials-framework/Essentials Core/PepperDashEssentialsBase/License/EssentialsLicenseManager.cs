using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronDataStore;
using PepperDash.Essentials.Core;
using PepperDash.Core;


namespace PepperDash.Essentials.License
{
    public abstract class LicenseManager
    {
        public BoolFeedback LicenseIsValid { get; protected set; }
        public StringFeedback LicenseMessage { get; protected set; }
        public StringFeedback LicenseLog { get; protected set; }

        protected LicenseManager()
        {
            CrestronConsole.AddNewConsoleCommand(
                s => CrestronConsole.ConsoleCommandResponse(GetStatusString()),
                "licensestatus", "shows license and related data",
                ConsoleAccessLevelEnum.AccessOperator);
        }

        protected abstract string GetStatusString();
    }

    public class MockEssentialsLicenseManager : LicenseManager
    {
        /// <summary>
        /// Returns the singleton mock license manager for this app
        /// </summary>
        public static MockEssentialsLicenseManager Manager
        {
            get
            {
                if (_Manager == null)
                    _Manager = new MockEssentialsLicenseManager();
                return _Manager;
            }
        }

        private static MockEssentialsLicenseManager _Manager;

        private bool IsValid;

        private MockEssentialsLicenseManager() : base()
        {
            LicenseIsValid = new BoolFeedback("LicenseIsValid",
                () => { return IsValid; });
            CrestronConsole.AddNewConsoleCommand(
                s => SetFromConsole(s.Equals("true", StringComparison.OrdinalIgnoreCase)),
                "mocklicense", "true or false for testing", ConsoleAccessLevelEnum.AccessOperator);

            bool valid;
            CrestronDataStore.CDS_ERROR err = CrestronDataStoreStatic.GetGlobalBoolValue("MockLicense", out valid);
            if (err == CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
                SetIsValid(valid);
            else if (err == CrestronDataStore.CDS_ERROR.CDS_RECORD_NOT_FOUND)
                CrestronDataStoreStatic.SetGlobalBoolValue("MockLicense", false);
            else
                CrestronConsole.PrintLine("Error restoring Mock License setting: {0}", err);
        }

        private void SetIsValid(bool isValid)
        {
            IsValid = isValid;
            CrestronDataStoreStatic.SetGlobalBoolValue("MockLicense", isValid);
            Debug.Console(0, "Mock License is{0} valid", IsValid ? "" : " not");
            LicenseIsValid.FireUpdate();
        }

        private void SetFromConsole(bool isValid)
        {
            SetIsValid(isValid);
        }

        protected override string GetStatusString()
        {
            return string.Format("License Status: {0}", IsValid ? "Valid" : "Not Valid");
        }
    }
}