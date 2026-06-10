namespace PepperDash.Essentials.Devices.Common.DSP.QscDsp
{
    public abstract class QscDspControlPoint : DspControlPoint
    {
        public string Key { get; protected set; }

        public string LevelInstanceTag { get; private set; }
        public string MuteInstanceTag { get; private set; }
        protected QscDsp Parent { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="levelInstanceTag">level named control/instance tag</param>
        /// <param name="muteInstanceTag">mute named control/instance tag</param>
        /// <param name="parent">parent DSP instance</param>
        protected QscDspControlPoint(string levelInstanceTag, string muteInstanceTag, QscDsp parent)
        {
            LevelInstanceTag = levelInstanceTag;
            MuteInstanceTag = muteInstanceTag;
            Parent = parent;
        }

        /// <summary>
        /// Initializes the plugin
        /// </summary>
        public void Initialize()
        {
        }

        /// <summary>
        /// Sends a command to the DSP
        /// </summary>
        /// <param name="cmd">command</param>
        /// <param name="instance">named control/instance tag</param>
        /// <param name="value">value (use "" if not applicable)</param>
        protected void SendFullCommand(string cmd, string instance, string value)
        {
            string cmdToSend = string.Format("{0} \"{1}\" {2}", cmd, instance, value);
            Parent.SendLine(cmdToSend);
        }

        /// <summary>
        /// Parses get message return
        /// </summary>
        /// <param name="attributeCode">attribute code</param>
        /// <param name="message">message</param>
        protected void ParseGetMessage(string attributeCode, string message)
        {
        }


        /// <summary>
        /// Sends the subscription command of the instance tag for the provided change group
        /// </summary>
        /// <param name="instanceTag">named control/instance tag</param>
        protected void SendSubscriptionCommand(string instanceTag)
        {
            // Subscription string format: InstanceTag subscribe attributeCode Index1 customName responseRate
            // Ex: "RoomLevel subscribe level 1 MyRoomLevel 500"

            string cmd = string.Format("cga 1 \"{0}\"", instanceTag);

            Parent.SendLine(cmd);
        }
    }
}