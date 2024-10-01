using System;
using PepperDash.Core;


namespace PepperDash.Essentials.Core.Presets
{
    public class PresetsListSubpageReferenceListItem : SubpageReferenceListItem
    {
        DevicePresetsView View;
        PresetChannel Channel;

        public PresetsListSubpageReferenceListItem(PresetChannel chan, uint index,
            SubpageReferenceList owner, DevicePresetsView view)
            : base(index, owner)
        {
            View = view;
            Channel = chan;
            owner.GetBoolFeedbackSig(index, 1).UserObject = new Action<bool>(b =>
            {
                if (!b) view.Model.Dial((int)index);
            });
            Refresh();
        }

        public override void Clear()
        {
            Owner.GetBoolFeedbackSig(Index, 1).UserObject = null;
            Owner.StringInputSig(Index, 1).StringValue = "";
            Owner.StringInputSig(Index, 2).StringValue = "";
            Owner.StringInputSig(Index, 3).StringValue = "";
        }

        public override void Refresh()
        {
            string name = View.ShowName ? Channel.Name : "";
            Owner.StringInputSig(Index, 1).StringValue = name;
            string chan = View.ShowNumbers ? Channel.Channel : "";
            Owner.StringInputSig(Index, 2).StringValue = chan;
            string url = View.Model.ImagesLocalHostPrefix + View.Model.ImagesPathPrefix + Channel.IconUrl;
            Debug.Console(2, "icon url={0}", url);
            string icon = View.ShowIcon ? url : "";
            Owner.StringInputSig(Index, 3).StringValue = icon;
        }
    }
}