namespace PepperDash.Essentials.Core.Shades
{
    /// <summary>
    /// Base class for a shade device
    /// </summary>
    public abstract class ShadeBase : EssentialsDevice, IShadesOpenCloseStop
    {
        public ShadeBase(string key, string name)
            : base(key, name)
        {
        }

        #region iShadesOpenClose Members

        public abstract void Open();
        public abstract void Stop();
        public abstract void Close();

        #endregion
    }
}