using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows.Forms;

/* DTXChip IDs
 * #---1A Left Cymbal
 * #---11 Hihat
 * #---18 Hihat Open
 * #---1B Left Pedal
 * #---1C Left Bass Drum (DTX only)
 * #---12 Snare
 * #---14 High Tom
 * #---13 Bass Drum
 * #---15 Low Tom
 * #---17 Floor Tom
 * #---16 Right Cymbal
 * #---19 Ride Cymbal (DTX only)
 */

namespace DTXtoSSC {
    internal class SSCMeasure {
        private static int GCD(int A, int B) {
            while (A != B) {
                if (A > B)
                    A = A - B;
                if (B > A)
                    B = B - A;
            }
            return A;
        }
        private static int LCMMax(int A, int B) {
            int[] Result = { A * B / GCD(A, B), A, B };
            return Result.Max();
        }
        private int Precision = 4; // Precision = Number of chip raw lines in measure
        public enum Lane { LC, HH, LP, SN, HT, BD, LT, FT, RC }
        public List<char[]> Data = new List<char[]>();

        public SSCMeasure() {
            for (int i = 0; i < 4; i++) { // Always start with precision 4
                Data.Add(new char[] { '0', '0', '0', '0', '0', '0', '0', '0', '0' });
            }
        }
        public SSCMeasure(bool Debug) {
            for (int i = 1; i <= 4; i++) { // Initialize with "111111111", "222222222", "333333333", "444444444", 
                Data.Add(new char[] { (char)i, (char)i, (char)i, (char)i, (char)i, (char)i, (char)i, (char)i, (char)i });
            }
        }

        public void AddPrecision(int Value) {
            if (Value == Precision) {
                return;
            }

            //List<char[]> OldData = new List<char[]>(Data); // Debug before adding delimiting notes
            int OldPrecision = Precision;
            int NewPrecision = Precision = LCMMax(Value, OldPrecision); // Precision cannot be decreased
            int IndexSeparate = (NewPrecision / OldPrecision) - 1; // Amount of indexes to be added between every old index
            for (int FillIndex = Data.Count; FillIndex > 0; FillIndex--) { // Reverse iterate, filling in new holes of new precision
                for (int j = 0; j < IndexSeparate; j++) {
                    Data.Insert(FillIndex, new char[] { '0', '0', '0', '0', '0', '0', '0', '0', '0' }); // Fill with empty notes
                }
            }
            //List<char[]> NewData = new List<char[]>(Data); // Debug after adding delimiting notes
        }

        // To Do: Ensure DesiredPrecision is valid against current Precision (LCM?)
        public void SetNote(Lane Lane, char Value, int BeatPosition, int NotePrecision) {
            BeatPosition--; // Anti-Zero-Indexing; 3, 4 is beat 3
            int StepPrecision = Precision / NotePrecision;
            Data[BeatPosition * StepPrecision][(int)Lane] = Value;
        }

        public string ConcatenateRawLine(int Index) {
            return new string(Data[Index]);
        }

        public string ConcatenateMeasure() {
            string Complete = "";
            for(int LinePosition = 0; LinePosition < Data.Count; LinePosition++) {
                Complete += ConcatenateRawLine(LinePosition) + '\n';
            }
            return Complete;
        }
    }

    internal class SSCFile {
        private readonly List<SSCMeasure> Measures = new List<SSCMeasure>();
        private string Title, Artist; // To Do

        // Get amount of measures in file
        public int Length() => Measures.Count;

        // Adds a single measure to the the end of the file
        public int AddMeasure() {
            Measures.Add(new SSCMeasure());
            return Measures.Count;
        }

        // Extends the current simfile length to value
        // Value less than actual count does not remove measures
        public void SetMeasures(int Count) {
            int MeasuresNeeded = Count - Measures.Count;
            for (int Bar = 0; Bar <= MeasuresNeeded; Bar++) {
                AddMeasure();
            }
        }

        public SSCMeasure GetMeasure(int Index) {
            return Measures[Index];
        }

        public void WriteDialogToSSC() {
            string Path;
            using (SaveFileDialog Dialog = new SaveFileDialog()) {
                Dialog.Filter = "SSC file (*.ssc)|*.ssc";
                Dialog.ShowDialog();
                if (string.IsNullOrEmpty(Dialog.FileName))
                    return;

                Path = Dialog.FileName;
            }
            WriteToSSC(Path);
        }

        public void WriteToSSC(string TargetPath) {
            using (StreamWriter File = new StreamWriter(TargetPath)) {
                File.WriteLine(@"//-----------gddm-new - DTXtoSSC------------");
                File.WriteLine(@"#NOTEDATA:;");
                File.WriteLine(@"#STEPSTYPE:gddm-new;");
                File.WriteLine(@"#NOTES:");
                for (int MeasureNumber = 0; MeasureNumber < Length(); MeasureNumber++) {
                    if (MeasureNumber == 0)
                        File.WriteLine(@"  // measure " + MeasureNumber.ToString());
                    else
                        File.WriteLine(@",  // measure " + MeasureNumber.ToString());

                    File.Write(Measures[MeasureNumber].ConcatenateMeasure());
                }
                File.WriteLine(@";");
            }
        }

    }

    internal class Program {

        public static readonly string[] _ChipIDs = { "#{0}1A", "#{0}11", "#{0}18", "#{0}1B", "#{0}1C", "#{0}12", "#{0}14", "#{0}13", "#{0}15", "#{0}17", "#{0}16", "#{0}19" };

        public static bool IsLaneChip(string Line) {
            foreach (string Chip in _ChipIDs) {
                return Regex.IsMatch(Line, @"^\s*" + string.Format(Chip, @"\d\d\d")); // Pattern ^\s*#\d\d\d1A to match LC
            }
            return false;
        }

        public static string GetChipLane(string Line) {
            string Head = Regex.Match(Line, @"^\s*#\d\d\d[0-9A-Z]{2}").Value; // Returns "#123AB"
            if (string.IsNullOrEmpty(Head)) {
                return string.Empty;
            }
            switch (Head.Substring(Head.Length - 2).ToUpper())
            {
                case "1A": return "LC"; // Left Cymbal
                case "11": return "HH"; // Hihat
                case "18": return "HH"; // Hihat Open -> Hihat
                case "1B": return "LP"; // Left Pedal
                case "1C": return "LP"; // Left Bass Drum -> Left Pedal
                case "12": return "SN"; // Snare
                case "14": return "HT"; // High Tom
                case "13": return "BD"; // Bass Drum
                case "15": return "LT"; // Low Tom
                case "17": return "FT"; // Floor Tom
                case "16": return "RC"; // Right Cymbal
                case "19": return "RC"; // Ride Cymbal -> Right Cymbal
                case "08": return "BPM"; // Tempo change
                default: return string.Empty;
            };
        }

        public static int GetMeasureNumber(string Line) {
            string PartialHead = Regex.Match(Line, @"^\s*#\d\d\d").Value; // Returns "#123"
            if (string.IsNullOrEmpty(PartialHead)) {
                throw new Exception("Invalid Chip");
            }
            return int.Parse(PartialHead.Substring(PartialHead.Length - 3));
        }

        public static List<string> GetChipData(string Line) { // Length of list changes inversely with note value of measure
            string RawData = Regex.Matches(Line, @":\s*(([0-9A-Z]{2})+)")[0].Groups[1].Value;
            return (from Match m in Regex.Matches(RawData, @"..") select m.Value).ToList();
        }

        [STAThread]
        public static void Main(string[] args) {

            SSCFile DTXtoSSCFile = new SSCFile();
            string DTXFilePath;

            // Get DTX File
            using (OpenFileDialog Dialog = new OpenFileDialog()) {
                Dialog.Filter = "DTX file (*.dtx)|*.dtx";
                Dialog.ShowDialog();
                if (string.IsNullOrEmpty(Dialog.FileName))
                    return;

                DTXFilePath = Dialog.FileName;
            }

            List<string> DTXFile = new List<string>(File.ReadAllLines(DTXFilePath));
            
            // Test
            /*List<string> DTXFile = new List<string>{
                "#00311: 04000404040404040404040404040404", // Hihat (Enum 1) 1/32
                "#0031A: 01010101", // Left Cymbal (Enum 0) 1/4
                "#00312: 0001" // Snare (Enum 3) 1/2
            };*/

            // Parse through .dtx
            for (int LineIndex = 0; LineIndex < DTXFile.Count; LineIndex++) {
                int EditorLineIndex = LineIndex + 1;
                string Line = DTXFile[LineIndex].ToUpper();
                string ChipLane = GetChipLane(Line); // string.Empty if invalid non-lane chip

                if (ChipLane == "BPM") { // Look at BPM chip
                    // do nothing for now
                }
                else if (!string.IsNullOrEmpty(ChipLane)) { // Only look at Lane chips (the ones you hit)

                    int MeasureNumber = GetMeasureNumber(Line);
                    List<string> Data = GetChipData(Line);
                    int Precision = Data.Count;

                    DTXtoSSCFile.SetMeasures(MeasureNumber); // Ensure enough measures exist
                    SSCMeasure Measure = DTXtoSSCFile.GetMeasure(MeasureNumber);
                    SSCMeasure.Lane LaneType = (SSCMeasure.Lane)Enum.Parse(typeof(SSCMeasure.Lane), ChipLane, true); // "BD" -> SSCMeasure.Lane.BD (int 6)

                    Measure.AddPrecision(Precision);

                    for(int DivisionPosition = 1; DivisionPosition <= Data.Count; DivisionPosition++) {
                        char Value = Data[DivisionPosition - 1] != "00" ? '1' : '0'; // DTX Chip = 00 means no chip in position
                        Measure.SetNote(LaneType, Value, DivisionPosition, Precision);
                    }

                }
            }

            // Save SSC
            DTXtoSSCFile.WriteDialogToSSC();
        }
    }
}
