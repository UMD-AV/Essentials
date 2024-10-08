using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class DefaultRecordingFolder
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class RecoderInfo
    {
        public RemoteRecorderState State { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DefaultRecordingFolder DefaultRecordingFolder { get; set; }

        public RecoderInfo()
        {
            DefaultRecordingFolder = new DefaultRecordingFolder();
            State = RemoteRecorderState.Unknown;
        }
    }

    public class RemoteRecoderSearchResult
    {
        public List<RecoderInfo> Results { get; set; }
    }
}