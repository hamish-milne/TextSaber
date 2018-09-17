namespace TextSaber.JSONModel
{
	using System.Collections.Generic;

	public enum BSEnvironment
	{
		DefaultEnvironment,
		BigMirror,
		TriangleEnvironment,
		NiceEnvironment
	}

	public enum BSDifficulty
	{
		Easy,
		Normal,
		Hard,
		Expert,
		ExpertPlus,
	}

	public class SongInfo
	{
		public string songName;
		public string songSubName;
		public string authorName;
		public double beatsPerMinute;
		public double previewStartTime;
		public double previewDuration;
		public string coverImagePath;
		public BSEnvironment environmentName;
		public List<LevelInfo> difficultyLevels = new List<LevelInfo>();

	}

	public class LevelInfo
	{
		public BSDifficulty difficulty;
		public int difficultyRank;
		public string audioPath;
		public string jsonPath;
		public double offset;
		public double oldOffset;
	}

	public class LevelData
	{
		public string _version = "1.5.0";
		public double _beatsPerMinute;
		public int _beatsPerBar = 16;
		public double _noteJumpSpeed = 10.0;
		public int _shuffle = 0;
		public double _shufflePeriod = 0.5;
		public List<Event> _events = new List<Event>();
		public List<Note> _notes = new List<Note>();
		public List<Obstacle> _obstacles = new List<Obstacle>();
	}

	public class Event
	{
		public double _time;
		public int _type;
		public int _value;
	}

	public class Note
	{
		public double _time;
		public int _lineIndex;
		public int _lineLayer;
		public int _type;
		public int _cutDirection;
	}

	public class Obstacle
	{
		public double _time;
		public int _lineIndex;
		public int _type;
		public double _duration;
		public int _width;
	}
}