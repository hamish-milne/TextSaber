using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace TextSaber
{
	class Program
	{
		const string FinalAudioName = "song.ogg";

		static void Main(string[] args)
		{
			Console.WriteLine("TextSaber converter, yay!");
			var inFile = args[0];
			var outDir = args[1];

			Directory.CreateDirectory(outDir);

			var frames = TextSaberFile.Parse(inFile, new ParserConfig()).ToArray();
			var config = new TextSaberFile.LevelConfig();
			var notes = TextSaberFile.EmitNotes(frames, config).ToArray();
			ProcessAudio(config, outDir);

			var info = new JSONModel.SongInfo
			{
				songName = config.Title,
				songSubName = config.SubTitle,
				authorName = config.Author,
				beatsPerMinute = config.BPM,
				// preview??
				// environmentName,
			};

			var coverImageSrc = Path.ChangeExtension(inFile, ".jpg");
			if (File.Exists(coverImageSrc))
			{
				const string coverImageDst = "cover.jpg";
				info.coverImagePath = coverImageDst;
				File.Copy(coverImageSrc, Path.Combine(outDir, coverImageDst));
			}

			foreach (var d in (JSONModel.BSDifficulty[])Enum.GetValues(typeof(JSONModel.BSDifficulty)))
			{
				var level = new JSONModel.LevelData
				{
					_beatsPerMinute = config.BPM,
					_notes = notes.OfType<Note>()
						.Where(n => (n.DifficultyMask & (1 << (int)d)) != 0)
						.Select(n => new JSONModel.Note
					{
						_time = n.Time,
						_cutDirection = (int)n.CutDirection,
						_type = (int)n.Color,
						_lineIndex = n.X,
						_lineLayer = n.Y
					}).ToList(),
					_obstacles = notes.OfType<Obstacle>()
						.Where(n => (n.DifficultyMask & (1 << (int)d)) != 0)
						.Select(n => new JSONModel.Obstacle
					{
						_time = n.Time,
						_width = Math.Abs(n.X2 - n.X1) + 1,
						_duration = n.Length,
						_lineIndex = Math.Min(n.X1, n.X2),
						_type = Math.Min(n.Y1, n.Y2) >= 2 ? 1 : 0
					}).ToList()
				};
				if (level._notes.Count > 0)
				{
					var jsonPath = $"{d}.json";
					File.WriteAllText(Path.Combine(outDir, jsonPath),
						Newtonsoft.Json.JsonConvert.SerializeObject(level));
					info.difficultyLevels.Add(new JSONModel.LevelInfo
					{
						difficulty = d,
						difficultyRank = (int)d, // TODO: check this??
						audioPath = FinalAudioName,
						offset = config.OffsetMs,
						oldOffset = config.OffsetMs, // TODO: check this??
					});
				}
			}
			File.WriteAllText(Path.Combine(outDir, "info.json"),
				Newtonsoft.Json.JsonConvert.SerializeObject(info));
		}

		static void ProcessAudio(TextSaberFile.LevelConfig config, string outDir)
		{
			if (string.IsNullOrEmpty(config.AudioFile))
			{
				return;
			}
			var ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");

			string firstInput = null;
			const string tmpSilence = "tmp_silence.ac3";
			if (config.AudioDelay > 0)
			{
				Process.Start(ffmpeg,
					$"-y -f lavfi -i anullsrc=cl=stereo -t {config.AudioDelay} {tmpSilence}");
				firstInput = $"-i {tmpSilence}";
			}

			const string codec = "-codec libvorbis -b:a 192k"; // TODO: Options for this
			// Copy or transcode to .ogg
			var newPath = Path.Combine(outDir, FinalAudioName);
			
			if (firstInput == null &&
				Path.GetExtension(config.AudioFile).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
			{
				File.Copy(config.AudioFile, newPath, true);
			} else {
				Process.Start(ffmpeg,
					$"-y {firstInput} -i \"{config.AudioFile}\" "
					+ $"-filter_complex 'concat=n=2:v=0:a=1[a]' -map '[a]' {codec} \"{newPath}\"")
					.WaitForExit();
				File.Delete(tmpSilence);
			}
			config.AudioFile = newPath;
		}
	}
}
