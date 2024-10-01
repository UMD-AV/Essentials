using System.Collections.Generic;
using Crestron.SimplSharp;
using PepperDash.Core;

namespace PepperDash.Essentials.Core.Fusion
{
    /// <summary>
    /// When created, runs progcomments on every slot and stores the program names in a list
    /// </summary>
    public class ProcessorProgReg
    {
        //public static Dictionary<int, ProcessorProgramItem> Programs { get; private set; }

        public static Dictionary<int, ProcessorProgramItem> GetProcessorProgReg()
        {
            Dictionary<int, ProcessorProgramItem> programs = new Dictionary<int, ProcessorProgramItem>();
            for (int i = 1; i <= Global.ControlSystem.NumProgramsSupported; i++)
            {
                string response = null;
                bool success = CrestronConsole.SendControlSystemCommand("progcomments:" + i, ref response);
                ProcessorProgramItem item = new ProcessorProgramItem();
                if (!success)
                    item.Name = "Error: PROGCOMMENTS failed";
                else
                {
                    if (response.ToLower().Contains("bad or incomplete"))
                        item.Name = "";
                    else
                    {
                        int startPos = response.IndexOf("Program File");
                        int colonPos = response.IndexOf(":", startPos) + 1;
                        int endPos = response.IndexOf(CrestronEnvironment.NewLine, colonPos);
                        item.Name = response.Substring(colonPos, endPos - colonPos).Trim();
                        item.Exists = true;
                        if (item.Name.Contains(".dll"))
                        {
                            startPos = response.IndexOf("Compiler Revision");
                            colonPos = response.IndexOf(":", startPos) + 1;
                            endPos = response.IndexOf(CrestronEnvironment.NewLine, colonPos);
                            item.Name = item.Name + "_v" + response.Substring(colonPos, endPos - colonPos).Trim();
                        }
                    }
                }

                programs[i] = item;
                Debug.Console(1, "Program {0}: {1}", i, item.Name);
            }

            return programs;
        }
    }

    /// <summary>
    /// Used in ProcessorProgReg
    /// </summary>
    public class ProcessorProgramItem
    {
        public bool Exists { get; set; }
        public string Name { get; set; }
    }
}