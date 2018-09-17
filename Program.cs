using System;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace TextSaber
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("TextSaber converter, yay!");

			var frames = TextSaberFile.Parse(args[0], new ParserConfig()).ToArray();
			var config = new TextSaberFile.LevelConfig();
			var notes = TextSaberFile.EmitNotes(frames, config).ToArray();

			var level = new JSONModel.LevelData
			{
				_beatsPerMinute = config.BPM,
				_notes = notes.OfType<Note>().Select(n => new JSONModel.Note
				{
					_time = n.Time,
					_cutDirection = (int)n.CutDirection,
					_type = (int)n.Color,
					_lineIndex = n.X,
					_lineLayer = n.Y
				}).ToList(),
				_obstacles = notes.OfType<Obstacle>().Select(n => new JSONModel.Obstacle
				{
					_time = n.Time,
					_width = Math.Abs(n.X2 - n.X1) + 1,
					_duration = n.Length,
					_lineIndex = Math.Min(n.X1, n.X2),
					_type = Math.Min(n.Y1, n.Y2) >= 2 ? 1 : 0
				}).ToList()
			};
			Console.WriteLine(config.OffsetMs);

			ProcessAudio(config, args[1]);
			File.WriteAllText($"{args[1]}/Hard.json", Newtonsoft.Json.JsonConvert.SerializeObject(level));
		}

		static void ProcessAudio(TextSaberFile.LevelConfig config, string outDir)
		{
			if (string.IsNullOrEmpty(config.AudioFile))
			{
				return;
			}
			var ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");
			const string codec = "-codec libvorbis -b:a 192k";
			// Copy or transcode to .ogg
			var newPath = Path.Combine(outDir, "song.ogg");
			if (Path.GetExtension(config.AudioFile).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
			{
				File.Copy(config.AudioFile, newPath, true);
			} else {
				Process.Start(ffmpeg,
					$"-y -i \"{config.AudioFile}\" {codec} \"{newPath}\"")
					.WaitForExit();
			}
			config.AudioFile = newPath;

			// Add any intro silence
			if (config.AudioDelay > 0)
			{
				var originalPath = Path.Combine(outDir, "song_orig.ogg");
				File.Delete(originalPath);
				File.Move(newPath, originalPath);
				const string tmpSilence = "tmp_silence.ogg";
				Process.Start(ffmpeg,
					$"-y -f lavfi -i anullsrc=cl=1:r=44100 -t {config.AudioDelay} {codec} {tmpSilence}")
					.WaitForExit();
				Process.Start(ffmpeg,
					$"-y -i concat:\"{tmpSilence}|{originalPath}\" {codec} \"{newPath}\"")
					.WaitForExit();
				File.Delete(originalPath);
				File.Delete(tmpSilence);
			}
		}
	}
}
