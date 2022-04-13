using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript {
   partial class Program : MyGridProgram {

      // This file contains your actual script.
      //
      // You can either keep all your code here, or you can create separate
      // code files to make your program easier to navigate while coding.
      //
      // In order to add a new utility class, right-click on your project, 
      // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
      // category under 'Visual C# Items' on the left hand side, and select
      // 'Utility Class' in the main area. Name it in the box below, and
      // press OK. This utility class will be merged in with your code when
      // deploying your final script.
      //
      // You can also simply create a new utility class manually, you don't
      // have to use the template if you don't want to. Just do so the first
      // time to see what a utility class looks like.
      // 
      // Go to:
      // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
      //
      // to learn more about ingame scripts.

      #region mdk preserve
      // Define Mode operation of current Programmable block
      // One of [X, Y, Z]
      readonly char Mode = 'Z';

      // Define PB group identifier
      readonly string ID = "Omega";

      // All Other configuration must be done in CustomData of the programmable blocks.
      // It will get synced with other programmable blocks with the same ID!

      /////////////////////DO NOT MODIFY BELOW!!!//////////////////////
      
      /////////////////////DO NOT MODIFY BELOW!!!//////////////////////
      
      /////////////////////DO NOT MODIFY BELOW!!!//////////////////////



      #endregion


      SimpleTimerSM timerSM;
      MyIni _ini = new MyIni();
      Random rand = new Random();
      List<string> PosList = null;
      string StartPosition;
      string CurrPosition;
      string CurrTask;
      ushort Timeout;
      string[] Settings;
      string ERROR;
      bool _Arrived;
		double Velocity;
		string StatusMessage;
      string OperationMode;

      List<string> Booths = null;
      string Exit = "";

      //Global (Sensor) objects for ToPosition()
      IMyRemoteControl Remote = null;
		IMySensorBlock ZSens1 = null;
      List<MyDetectedEntityInfo> ZS1List = null;
      IMySensorBlock ZSens2 = null;
      List<MyDetectedEntityInfo> ZS2List = null;
      List<IMyMotorSuspension> Wheels = null;
		int toMove;

      //Global 
      readonly string[] StatusSpinner = new string[4] { "[_]", "[\\]", "[I]", "[/]" };
		string StatusHeader;
		ushort StatusSpinnerC;
      ushort TimeoutC = 0;
		bool PosCounted = false;
		private List<string> TaskList;

		public Program() {

         StatusHeader = StatusSpinner[StatusSpinnerC] + " [CES] CraneElevatorScript\n";
         StatusSpinnerC++;
         StatusMessage = StatusHeader + "<Init>";
         Echo(StatusMessage);
         StatusMessage = "";

         Conf('R');

         Runtime.UpdateFrequency = (UpdateFrequency.Update10 | UpdateFrequency.Update100);

         List<IMyRemoteControl> RemotesList = null;
         GridTerminalSystem.GetBlocksOfType<IMyRemoteControl>(RemotesList, x => x.CubeGrid == Me.CubeGrid);
         Remote = RemotesList[0];

         /// Get Sensors for Floor (Z Axis) sensing
         List<IMyTerminalBlock> ZSensor1L = null;
         List<IMyTerminalBlock> ZSensor2L = null;
         GridTerminalSystem.SearchBlocksOfName("FS1", ZSensor1L, x => x.CubeGrid == Me.CubeGrid && x.DisplayNameText.ToLower().Contains("sensor"));
         GridTerminalSystem.SearchBlocksOfName("FS2", ZSensor2L, x => x.CubeGrid == Me.CubeGrid && x.DisplayNameText.ToLower().Contains("sensor"));
         ZSens1 = ZSensor1L[0] as IMySensorBlock;
         ZSens2 = ZSensor2L[0] as IMySensorBlock;

         GridTerminalSystem.GetBlockGroupWithName("Suspension Crane Z").GetBlocksOfType<IMyMotorSuspension>(Wheels, x => x.CubeGrid == Me.CubeGrid);

         TaskList.Add("M" + CurrPosition);

      }

		private bool Conf(char Mode = 'R') {
         MyIniParseResult result;
         if (!_ini.TryParse(Me.CustomData, out result))
            throw new Exception(result.ToString());

         switch (Mode) {
            case 'W':

               Storage = string.Join(";",
                  ERROR ?? "",
                  CurrTask ?? "",
                  CurrPosition ?? "ERROR"
               );

               _ini.Set("CES Settings", "TimeOut", Timeout);
               _ini.SetComment("CES Settings", "TimeOut", "The Timeout in seconds until an Error is assumed - raise for larger systems!");
               _ini.Set("CES Settings", "ExitPos", Exit);
               _ini.SetComment("CES Settings", "ExitPos", "Elevator position that is assumed as the entry/exit of the system.");
               _ini.Set("CES Settings", "Positions", "\n|" + string.Join("\n|", PosList));
               _ini.SetComment("CES Settings", "Positions", "List of positions, the user set up for the elevator. Format: \nLetters for Center/Front/Back/Left/Right, followed by (negative) number e.g:\nPositions=\n|F2\n|B-1");

               _ini.Set("TaskList", "Tasks", "\n|" + string.Join("\n|", TaskList));
               break;
            default: // 'R'

               Settings = Storage.Split(';');
               ERROR = Settings[0] ?? "";
               CurrTask = Settings[1] ?? "";
               CurrPosition = Settings[2] ?? "C0";

               Timeout = _ini.Get("CES Settings", "TimeOut").ToUInt16(15);
               Exit = _ini.Get("CES Settings", "ExitPos").ToString("");
               _ini.Get("CES Settings", "Positions").GetLines(PosList);
               _ini.Get("TaskList", "Tasks").GetLines(TaskList);
               break;
         }
         return true;
		}

		private void ArgHandler(string Arg) {
         CurrTask = Arg;
         switch (Arg.Substring(1, 1).ToUpper()) {
            case "M":
               TaskList.Insert(0, Arg.Substring(2));
               break;
            case "S":
               //TODO: check if Booth empty!
               TaskList.Insert(0, "M" + CurrPosition);
               TaskList.Insert(0, "G");
               TaskList.Insert(0, "M" + Arg.Substring(2));
               TaskList.Insert(0, "L");
               break;
            case "R":
               TaskList.Insert(0, "M" + Arg.Substring(2));
               TaskList.Insert(0, "G");
               TaskList.Insert(0, "M" + Exit);
               break;
            case "G":
               TaskList.Insert(0, "M" + CurrPosition);
               TaskList.Insert(0, "G");
               break;
            case "L":
               TaskList.Insert(0, "M" + CurrPosition);
               TaskList.Insert(0, "L");
               break;
            default:
               StatusMessage += "Wrong argument. One of M[]/S[]/R[]/G/L";
               break;
         }
      }

      private void RunTask(string Task) {
         switch (Task.Substring(1, 1)) {
            case "M":
               ToPosition(Task.Substring(2));
               break;
            case "G":
               Grab();
               break;
            case "L":
               LetGo();
               break;
            case "R":
               Storage = "";
               Me.CustomData = "";
               ERROR = "Script reset. Reompile!";
               break;
         }
      }

      void Grab() {
         //(send) Move CraneArm down until ArmSensor active, lock magnetic plate; then move arm up.
         /// maybe remember Extension of CraneArm
		}
      void LetGo() {
         //(send) Move CraneArm down while below ExtensionCraneArm, unlock magnetic plate; then move arm up.
		}

		private void ToPosition(string Position) {
         // Try to move to stored position. Dont rely on moving being done after this runs!!
         // Accepts string like "f-1" or "B10"

         if (!PosList.Contains(Position)) {
            ERROR = "Position not configured!";
            return;
			}
         
         short Floor = 0;
         string Booth;
         /// Split string into Floor and Center/front/back/left/right
         try {
            Floor = Int16.Parse(Position.Substring(2));
         } catch (FormatException) {
            //catch wrong argument
			}
         Booth = Position.Substring(1, 1).ToUpper() ?? "C";

         /// Send to other PBs first, wait for response, make sure self is last link in chain.
         /// (Maybe work with Mode letters)

         /// Set Height Wheel velocity in according direction
         short CurrFloor = 0;
         string CurrBooth;
         /// Split string into Floor and Center/front/back/left/right
         try {
            CurrFloor = Int16.Parse(CurrPosition.Substring(2));
         } catch (FormatException) {
         }
         CurrBooth = Position.Substring(1, 1).ToUpper();
         toMove = CurrFloor - Floor;
         if (CurrBooth != "C") {
            //TODO: Move to center
			}

         //TODO: Override Override when in gravity. (Slowly lower from 
         int Override = toMove.CompareTo(0);
         if (Remote.GetTotalGravity().Length() < 0.2) {
            //TODO: Gradually Override until close to maxSpeed
			}


         foreach(var Susp in Wheels) {
            Susp.PropulsionOverride = Override;
            Susp.Brake = true;
			}

         StartPosition = CurrPosition;
         timerSM = new SimpleTimerSM(this, ArriveSeq());
         timerSM.Start();

         /// Set Dispositon Wheel velocity in according direction
         /// Send command to craneArm!

         return;
		}

      IEnumerable<double> ArriveSeq() {
         _Arrived = false;
         int TimeOutMult = 1;
         float Override = toMove.CompareTo(0) / 20;

         foreach (var Susp in Wheels)
            (Susp as IMyTerminalBlock).ApplyAction("OnOff_On");

         while (!_Arrived && (TimeoutC < (Timeout * TimeOutMult))) {
            Velocity = Remote.GetShipSpeed();
            TimeoutC++;
            //TODO: Use relative speed against main grid
            if (Velocity < 0.2) { // Apply more and more Override
               foreach (var Susp in Wheels) {
                  Susp.PropulsionOverride += Override;
               }
               TimeOutMult = 2;
               yield return 0.5;
            } else {
               ZSens1.DetectedEntities(ZS1List);
               ZSens2.DetectedEntities(ZS2List);
               if ((ZS1List.Count != 0) && (ZS2List.Count != 0) && !PosCounted) {
                  toMove += toMove.CompareTo(0);
                  PosCounted = true;
                  //TODO: update CurrPosition
                  if (toMove == 0) {
                     _Arrived = true;
                     break;
                  }
               }
               TimeOutMult = 5;
               yield return 0.2;
            }
         }
         foreach (var Susp in Wheels) {
            Susp.PropulsionOverride = 0;
         }
         Velocity = 0;
         CurrTask = "";
      }

      IEnumerable<double> ArriveSeqOld() {
         _Arrived = false;
         TimeoutC++;
         while (!_Arrived && TimeoutC < Timeout*2) {
            ZSens1.DetectedEntities(ZS1List);
            ZSens2.DetectedEntities(ZS2List);
            if ((ZS1List.Count != 0) && (ZS2List.Count != 0)) {
               toMove += toMove.CompareTo(0);
               //TODO: Maybeeeeee update CurrPosition
               if (toMove == 0) {
                  _Arrived = true;
                  break;
               }
            }
            yield return 0.5; // wait 1 second.
         }
         foreach (var Susp in Wheels) {
            Susp.IsParkingEnabled = true;
            (Susp as IMyTerminalBlock).ApplyAction("OnOff_Off");
         }
      }

		public void Save() {
         // Called when the program needs to save its state. Use
         // this method to save your state to the Storage field
         // or some other means. 
         // 
         // This method is optional and can be removed if not
         // needed.

         Conf('W');

      }

      public void Main(string argument, UpdateType updateSource) {
         // The main entry point of the script, invoked every time
         // one of the programmable block's Run actions are invoked,
         // or the script updates itself. The updateSource argument
         // describes where the update came from. Be aware that the
         // updateSource is a  bitfield  and might contain more than 
         // one update type.
         // 
         // The method itself is required, but the arguments above
         // can be removed if not needed.
         if (StatusSpinnerC > StatusSpinner.Length - 1)
            StatusSpinnerC = 0;
         StatusMessage += CurrPosition + "\n";

         if (argument != "STOP") {
            if (argument == "RESET") {
               STOPPER();
               RunTask("R");
				}
            if (ERROR != "") {
               ArgHandler(argument);
               timerSM.Run();
               if ((updateSource & UpdateType.Once) != 0) {

               }
               if (timerSM.Running != true) {
                  if ((updateSource & UpdateType.Terminal) != 0) {

                  }
                  if ((updateSource & UpdateType.Trigger) != 0) {

                  }
                  /*if ((updateSource & (1 << 2)) != 0) { // UpdateType.Antenna: //1 << 2

                  }*/
                  if ((updateSource & UpdateType.Update10) != 0) {

                  }
                  if ((updateSource & UpdateType.Update100) != 0) {
                     // Load Balancing, adjust Runtime.UpdateFrequency
                  }
               } else {
                  RunTask(TaskList[TaskList.Count - 1]);
                  TaskList.RemoveAt(TaskList.Count - 1);
                  StatusMessage += $"{StatusSpinner[StatusSpinnerC + 1]} Waiting for actions: ({(Timeout * 2) - TimeoutC})";
                  foreach (var Task in TaskList) {
							StatusMessage += $"\n {StatusSpinner[StatusSpinnerC + 2]} {Task} [{toMove}]";
                  }
                  
               }
            } else {
               Runtime.UpdateFrequency = UpdateFrequency.None;
               StatusMessage = "ERROR: " + ERROR;
            }
         } else {
            STOPPER();
			}

         StatusHeader = StatusSpinner[StatusSpinnerC] + " [CES] CraneElevatorScript\n";
         StatusSpinnerC++;
         StatusMessage = StatusHeader + StatusMessage;
         Echo(StatusMessage);
         StatusMessage = "";
      }

		private void STOPPER() {
         //TODO: Send STOP to others!!
         foreach (var Susp in Wheels) {
            Susp.IsParkingEnabled = true;
            (Susp as IMyTerminalBlock).ApplyAction("OnOff_Off");
         }
         Runtime.UpdateFrequency = UpdateFrequency.None;
         StatusMessage += "USER ISSUED STOP!\n Recompile.";
      }



		/// <summary>
		/// Quick usage:
		/// <para>1. A persistent instance for each sequence you want to run in parallel.</para>
		/// <para>2. Create instance(s) in Program() and execute <see cref="Run"/> in Main().</para>
		/// </summary>
		public class SimpleTimerSM {
         public readonly Program Program;

         /// <summary>
         /// Wether the timer starts automatically at initialization and auto-restarts it's done iterating.
         /// </summary>
         public bool AutoStart { get; set; }

         /// <summary>
         /// Returns true if a sequence is actively being cycled through.
         /// False if it ended or no sequence is assigned anymore.
         /// </summary>
         public bool Running { get; private set; }

         /// <summary>
         /// Setting this will change what sequence will be used when it's (re)started.
         /// </summary>
         public IEnumerable<double> Sequence;

         /// <summary>
         /// Time left until the next part is called.
         /// </summary>
         public double SequenceTimer { get; private set; }

         private IEnumerator<double> sequenceSM;

         public SimpleTimerSM(Program program, IEnumerable<double> sequence = null, bool autoStart = false) {
            Program = program;
            Sequence = sequence;
            AutoStart = autoStart;

            if (AutoStart) {
               Start();
            }
         }

         /// <summary>
         /// (Re)Starts sequence, even if already running.
         /// Don't forget <see cref="IMyGridProgramRuntimeInfo.UpdateFrequency"/>.
         /// </summary>
         public void Start() {
            SetSequenceSM(Sequence);
         }

         /// <summary>
         /// <para>Call this in your <see cref="Program.Main(string, UpdateType)"/> and have a reasonable update frequency, usually Update10 is good for small delays, Update100 for 2s or more delays.</para>
         /// <para>Checks if enough time passed and executes the next chunk in the sequence.</para>
         /// <para>Does nothing if no sequence is assigned or it's ended.</para>
         /// </summary>
         public void Run() {
            if (sequenceSM == null)
               return;

            SequenceTimer -= Program.Runtime.TimeSinceLastRun.TotalSeconds;

            if (SequenceTimer > 0)
               return;

            bool hasValue = sequenceSM.MoveNext();

            if (hasValue) {
               SequenceTimer = sequenceSM.Current;

               if (SequenceTimer <= -0.5)
                  hasValue = false;
            }

            if (!hasValue) {
               if (AutoStart)
                  SetSequenceSM(Sequence);
               else
                  SetSequenceSM(null);
            }
         }

         private void SetSequenceSM(IEnumerable<double> seq) {
            Running = false;
            SequenceTimer = 0;

            sequenceSM?.Dispose();
            sequenceSM = null;

            if (seq != null) {
               Running = true;
               sequenceSM = seq.GetEnumerator();
            }
         }
      }
   }
}
