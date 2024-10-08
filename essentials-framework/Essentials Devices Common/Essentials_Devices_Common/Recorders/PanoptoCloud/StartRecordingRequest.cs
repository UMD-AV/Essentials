using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class Recorder
    {
        public Guid RemoteRecorderId { get; set; }
    }

    public class StartRecordingRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Guid FolderId { get; set; }
        public List<Recorder> Recorders { get; set; }
    }
}