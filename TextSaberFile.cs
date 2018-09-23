namespace TextSaber
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Collections.Generic;

	public struct Note
	{
		public double Time;
		public int DifficultyMask;
		public int X;
		public int Y;
		public NoteColor Color;
		public CutDirection CutDirection;
	}

	public struct Obstacle
	{
		public double Time;
		public int DifficultyMask;
		public int X1, Y1, X2, Y2;
		public double Length;
	}

	public class FileFrame
	{
		public Dictionary<string, string> VariableSets { get; } = new Dictionary<string, string>();
		public List<Note> Notes { get; } = new List<Note>();
		public List<Obstacle> Obstacles { get; } = new List<Obstacle>();
	}

	public enum CutDirection
	{
		Up,
		Down,
		Left,
		Right,
		UpLeft,
		UpRight,
		DownLeft,
		DownRight,
		Any,
	}

	public enum NoteColor
	{
		Red,
		Blue,
		Bomb
	}

	public class ParserConfig
	{
		public string Positions { get; set; } = "ZXCVASDFQWERzxcvasdfqwer";
		public string Directions { get; set; } = "824679135";
		public char Bomb { get; set; } = '*';
		public char Obstacle { get; set; } = '[';
		public char ObstacleEnd { get; set; } = ']';
	}

	public class TextSaberFile
	{

		public static IEnumerable<FileFrame> Parse(string path, ParserConfig config)
		{
			const int nWidth = 4;
			const int nHeight = 3;
			foreach (var line in File.ReadLines(path))
			{
				var frame = new FileFrame();
				var notes = line;
				var tokens = line.Split(';');
				if (tokens.Length >= 2)
				{
					notes = tokens[0];
					foreach (var t in tokens.Skip(1))
					{
						var vtokens = t.Split('=');
						frame.VariableSets[vtokens[0].Trim()] = vtokens[1].Trim();
					}
				}
				CutDirection? dir = null;
				List<char> obstacleDef = null;
				foreach (var c in notes.Where(c => !char.IsWhiteSpace(c)))
				{
					if (obstacleDef != null)
					{
						if (c == config.Obstacle)
						{
							throw new Exception("Already defining an obstacle");
						}
						else if (c == config.ObstacleEnd)
						{
							var a = config.Positions.IndexOf(obstacleDef[0]);
							var b = config.Positions.IndexOf(obstacleDef[1]);
							frame.Obstacles.Add(new Obstacle
							{
								X1 = a % nWidth,
								Y1 = (a / nWidth) % nHeight,
								X2 = b % nWidth,
								Y2 = (b / nWidth) % nHeight,
								Length = int.Parse(new string(obstacleDef.Skip(2).ToArray()))
							});
							obstacleDef = null;
						}
						else
						{
							obstacleDef.Add(c);
						}
						continue;
					}
					if (c == config.Obstacle)
					{
						obstacleDef = new List<char>();
						continue;
					}
					if (c == config.Bomb)
					{
						dir = null;
						continue;
					}
					var posIdx = config.Positions.IndexOf(c);
					var dirIdx = config.Directions.IndexOf(c);
					if (posIdx < 0 && dirIdx < 0)
					{
						throw new Exception("Unexpected character " + c);
					}
					if (dirIdx >= 0)
					{
						dir = (CutDirection)dirIdx;
					}
					else if (!dir.HasValue)
					{
						var x = (posIdx % nWidth);
						var y = (posIdx / nWidth) % nHeight;
						frame.Notes.Add(new Note{X = x, Y = y, Color = NoteColor.Bomb});
					}
					else
					{
						var color = (NoteColor)(posIdx / (nWidth * nHeight));
						var x = (posIdx % nWidth);
						var y = (posIdx / nWidth) % nHeight;
						frame.Notes.Add(new Note{X = x, Y = y, Color = color, CutDirection = dir.Value});
					}
				}

				yield return frame;
			}
		}

		public class EmissionState
		{
			public double Time { get; set; }
			public double Measure { get; set; } = 1;
			public double InitialBPM { get; set; } = double.NaN;
			public double CurrentBPM { get; set; } = double.NaN;

			public EmissionState Clone()
			{
				return new EmissionState
				{
					Time = Time,
					Measure = Measure,
					InitialBPM = InitialBPM,
					CurrentBPM = CurrentBPM
				};
			}
		}

		public class LevelConfig
		{
			public string AudioFile { get; set; }
			public string Title { get; set; }
			public string SubTitle { get; set; }
			public string Author { get; set; }
			public double OffsetMs { get; set; } = 0;
			public double AudioDelay { get; set; } = 0;
			public double BPM { get; set; } = double.NaN;
		}

		private static void SetString(FileFrame frame, string name, Action<string> set)
		{
			if (frame.VariableSets.TryGetValue(name, out var str))
			{
				set(str);
			}
		}

		private static void SetDouble(FileFrame frame, string name, Action<double> set)
		{
			if (frame.VariableSets.TryGetValue(name, out var str))
			{
				set(double.Parse(str));
			}
		}

		public static IEnumerable<object> EmitNotes(
			IEnumerable<FileFrame> frames,
			LevelConfig config,
			EmissionState state = null,
			Dictionary<string, FileFrame[]> procedures = null)
		{
			state = state ?? new EmissionState();
			bool allowDefinitions = procedures == null;
			if (allowDefinitions)
			{
				procedures = new Dictionary<string, FileFrame[]>();
			}
			var proceduresDefining = new List<(string name, int remaining)>();
			var frameA = frames.ToArray();
			
			for (int i = 0; i < frameA.Length; i++)
			{
				double frameOffset = 0;
				var frame = frameA[i];

				SetDouble(frame, "set_time", value => state.Time = config.AudioDelay + value);
				SetDouble(frame, "bpm", value => config.BPM = value);
				SetDouble(frame, "intro_silence", value => {
					state.Time += config.BPM * value / 60;
					config.AudioDelay += value;
				});
				SetString(frame, "audio_file", value => config.AudioFile = value);
				SetString(frame, "title", value => config.Title = value);
				SetString(frame, "subtitle", value => config.SubTitle = value);
				SetString(frame, "author", value => config.Author = value);
				SetDouble(frame, "audio_offset_ms", value => config.OffsetMs = value);
				SetDouble(frame, "measure", value => state.Measure = value);
				SetDouble(frame, "offset", value => state.Time += (1.0 / state.Measure) * value);
				SetDouble(frame, "frame_offset", value => frameOffset = value);
	
				if (allowDefinitions && frame.VariableSets.TryGetValue("proc", out var procArgs))
				{
					var tokens = procArgs.Split(',');
					var name = tokens[0].Trim();
					var length = tokens.Length == 1 ? 1 : int.Parse(tokens[1].Trim());
					procedures.Add(name, frameA.Skip(i).Take(length).ToArray());
				}
				if (frame.VariableSets.TryGetValue("proc_add", out var procName))
				{
					foreach (var n in EmitNotes(procedures[procName], config, state.Clone(), procedures))
					{
						yield return n;
					}
				}
				if (frame.VariableSets.TryGetValue("proc_ins", out var procName2))
				{
					foreach (var n in EmitNotes(procedures[procName2], config, state, procedures))
					{
						yield return n;
					}
				}

				var fTime = state.Time + frameOffset*(1.0/state.Measure);
				foreach (var note in frame.Notes)
				{
					var myNote = note;
					myNote.Time = fTime;
					yield return myNote;
				}
				foreach (var obstacle in frame.Obstacles)
				{
					var myOb = obstacle;
					myOb.Time = fTime;
					myOb.Length /= state.Measure;
					yield return myOb;
				}

				state.Time += 1.0 / state.Measure;
			}
		}
	}
}