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
				_notes = notes.Select(n => new JSONModel.Note
				{
					_time = n.time,
					_cutDirection = (int)n.Note.CutDirection,
					_type = (int)n.Note.Color,
					_lineIndex = n.Note.X,
					_lineLayer = n.Note.Y
				}).ToList()
			};
			Console.WriteLine(config.OffsetMs);

			ProcessAudio(config, args[1]);
			File.WriteAllText($"{args[1]}/Hard.json", Newtonsoft.Json.JsonConvert.SerializeObject(level));
		}

		static void ProcessAudio(TextSaberFile.LevelConfig config, string outDir)
		{
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
