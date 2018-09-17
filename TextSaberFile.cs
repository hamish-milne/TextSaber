namespace TextSaber
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Collections.Generic;

	public struct Note
	{
		public double Time;
		public int X;
		public int Y;
		public NoteColor Color;
		public CutDirection CutDirection;
	}

	public struct Obstacle
	{
		public double Time;
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
			public double OffsetMs { get; set; } = 0;
			public double AudioDelay { get; set; } = 0;
			public double BPM { get; set; } = double.NaN;
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
				if (frame.VariableSets.TryGetValue("bpm", out var newBpm))
				{
					state.InitialBPM = double.Parse(newBpm);
					config.BPM = state.InitialBPM;
				}
				if (frame.VariableSets.TryGetValue("intro_silence", out var introSilence))
				{
					var value = double.Parse(introSilence);
					state.Time += state.InitialBPM * value / 60;
					config.AudioDelay += value;
				}
				if (frame.VariableSets.TryGetValue("audio_file", out var audioFile))
				{
					config.AudioFile = audioFile;
				}
				if (frame.VariableSets.TryGetValue("audio_offset_ms", out var audioOffset))
				{
					config.OffsetMs = double.Parse(audioOffset);
				}
				if (frame.VariableSets.TryGetValue("measure", out var newMeasure))
				{
					state.Measure = double.Parse(newMeasure);
				}
				if (frame.VariableSets.TryGetValue("offset", out var offset))
				{
					state.Time += (1.0 / state.Measure) * double.Parse(offset);
				}
				if (frame.VariableSets.TryGetValue("frame_offset", out var newFrameOffset))
				{
					frameOffset = double.Parse(newFrameOffset);
				}
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