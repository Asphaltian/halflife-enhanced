using System;

namespace GoldSrc;

internal sealed class Vox( Func<string, byte[]> loadFile )
{
	public const int MixSpeed = 11025;

	private const int MaxVoxWords = 32;
	private const int MaxSentenceNames = 2048;
	private const int MaxChunkLength = 0x6400000;
	private const int RenderChunk = 512;
	private const int ChannelPitch = 100;

	private struct Word
	{
		public int Volume;
		public int Pitch;
		public int Start;
		public int End;
		public int CbTrim;
		public int SampleFrac;
		public int TimeCompress;
		public string Path;
	}

	private sealed class Cache
	{
		public int Length;
		public int Speed;
		public int Width;
		public sbyte[] Data8;
		public short[] Data16;
	}

	public readonly List<(string Name, string Text)> Sentences = [];

	private readonly Dictionary<string, Cache> _caches = new( StringComparer.OrdinalIgnoreCase );

	private Word _wordDefault;

	public void ReadSentenceFile( string file )
	{
		var position = 0;

		Sentences.Clear();

		while ( position < file.Length && Sentences.Count < MaxSentenceNames )
		{
			while ( position < file.Length && file[position] is '\n' or '\r' or '\t' or ' ' )
				position++;

			if ( position >= file.Length )
				break;

			var nameStart = -1;
			var nameEnd = -1;

			if ( file[position] != '/' )
			{
				nameStart = position;

				while ( position < file.Length && file[position] is not (' ' or '\t') )
					position++;

				nameEnd = position;

				if ( position < file.Length )
					position++;
			}

			var textStart = position;

			while ( position < file.Length && file[position] is not ('\n' or '\r') )
				position++;

			var textEnd = position;

			if ( position < file.Length )
				position++;

			if ( nameStart >= 0 )
				Sentences.Add( (file[nameStart..nameEnd], file[textStart..textEnd]) );
		}
	}

	public short[] Render( string text, out int rate )
	{
		rate = MixSpeed;
		text = text.TrimStart( ' ', '\t' );

		var directory = "vox/";
		var slash = text.LastIndexOf( '/' );

		if ( slash >= 0 )
		{
			if ( slash + 1 > 31 )
				return null;

			directory = text[..(slash + 1)];
			text = text[(slash + 1)..];
		}

		if ( text.Length >= 512 )
			return null;

		var tokens = ParseString( text );
		var words = new List<Word>();
		var caches = new List<Cache>();

		for ( var i = 0; i < tokens.Count; i++ )
		{
			var token = tokens[i];

			if ( !ParseWordParams( ref token, out var word, i == 0 ) )
				continue;

			word.Path = $"{directory}{token}.wav";

			if ( word.Path.Length > 63 )
				word.Path = word.Path[..63];

			words.Add( word );
		}

		foreach ( var word in words )
		{
			if ( LoadSound( word.Path ) is not { } cache )
				break;

			caches.Add( cache );
			rate = Math.Max( rate, cache.Speed );
		}

		var output = new List<short>();
		var mixed = new List<short>();
		var last = 0;

		for ( var i = 0; i < caches.Count; i++ )
		{
			mixed.Clear();

			RenderWord( words[i], caches[i], mixed );

			if ( caches[i].Speed == rate )
			{
				output.AddRange( mixed );

				if ( mixed.Count > 0 )
					last = mixed[^1];

				continue;
			}

			foreach ( var sample in mixed )
			{
				output.Add( (short)((last + sample) >> 1) );
				output.Add( sample );

				last = sample;
			}
		}

		return output.Count == 0 ? null : [.. output];
	}

	private Cache LoadSound( string name )
	{
		if ( _caches.TryGetValue( name, out var cached ) )
			return cached;

		Cache cache = null;

		if ( loadFile( name[0] == '/' ? $"sound{name}" : $"sound/{name}" ) is { } data )
		{
			if ( GetWavInfo( data, out var rate, out var width, out var channels, out var samples, out var offset ) && channels == 1 && rate != 0 && offset < data.Length )
				cache = ResampleSfx( samples, rate, width, data, offset, data.Length - offset );
		}

		return _caches[name] = cache;
	}

	private static int FindChunk( byte[] data, int start, int end, string name )
	{
		var position = start;

		while ( position < end )
		{
			if ( position < 0 || position + 8 > data.Length )
				return -1;

			var length = BitConverter.ToInt32( data, position + 4 );

			if ( length < 0 )
				return -1;

			if ( name == "LIST" && end < position + 8 + length )
				return -1;

			if ( length > MaxChunkLength )
				return -1;

			var next = position + 8 + ((length + 1) & ~1);

			if ( data[position] == name[0] && data[position + 1] == name[1] && data[position + 2] == name[2] && data[position + 3] == name[3] )
				return position;

			position = next;
		}

		return -1;
	}

	private static bool GetWavInfo( byte[] data, out int rate, out int width, out int channels, out int samples, out int dataOffset )
	{
		rate = 0;
		width = 0;
		channels = 0;
		samples = 0;
		dataOffset = 0;

		var end = data.Length;
		var riff = FindChunk( data, 0, end, "RIFF" );

		if ( riff < 0 || riff + 12 > data.Length || data[riff + 8] != 'W' || data[riff + 9] != 'A' || data[riff + 10] != 'V' || data[riff + 11] != 'E' )
			return false;

		var start = riff + 12;
		var format = FindChunk( data, start, end, "fmt " );

		if ( format < 0 )
			return false;

		var position = format + 8;

		if ( BitConverter.ToInt16( data, position ) != 1 )
			return false;

		channels = BitConverter.ToInt16( data, position + 2 );
		rate = BitConverter.ToInt32( data, position + 4 );
		width = BitConverter.ToInt16( data, position + 14 ) / 8;

		if ( width == 0 )
			return false;

		var cue = FindChunk( data, start, end, "cue " );

		if ( cue >= 0 )
		{
			var loopStart = BitConverter.ToInt32( data, cue + 32 );
			var list = FindChunk( data, cue + 8 + ((BitConverter.ToInt32( data, cue + 4 ) + 1) & ~1), end, "LIST" );

			if ( list >= 0 && data[list + 28] == 'm' && data[list + 29] == 'a' && data[list + 30] == 'r' && data[list + 31] == 'k' )
				samples = loopStart + BitConverter.ToInt32( data, list + 24 );
		}

		var chunk = FindChunk( data, start, end, "data" );

		if ( chunk < 0 )
			return false;

		var count = BitConverter.ToInt32( data, chunk + 4 ) / width;

		if ( samples == 0 )
			samples = count;
		else if ( count < samples )
			return false;

		dataOffset = chunk + 8;

		return channels >= 0 && rate >= 0 && samples >= 0;
	}

	private static Cache ResampleSfx( int length, int rate, int width, byte[] data, int offset, int size )
	{
		var cache = new Cache { Length = length, Speed = rate, Width = width };
		var stepScale = (float)rate / MixSpeed;
		int count;

		if ( stepScale == 2f )
		{
			count = cache.Length;
			stepScale = 1f;
		}
		else
		{
			count = (int)(cache.Length / stepScale);

			cache.Length = count;
			cache.Speed = MixSpeed;
		}

		if ( cache.Width == 2 )
			cache.Data16 = new short[count];
		else
			cache.Data8 = new sbyte[count];

		if ( stepScale == 1f && width == 1 )
		{
			if ( size < count )
				return cache;

			for ( var i = 0; i < count; i++ )
				cache.Data8[i] = (sbyte)(data[offset + i] - 0x80);

			return cache;
		}

		var step = (int)(stepScale * 256f);

		if ( count > 0 )
		{
			var last = (uint)((count - 1) * step);

			if ( last > 0x7fffffff || (uint)size <= last >> 8 )
				return cache;
		}

		var fraction = 0;

		for ( var i = 0; i < count; i++ )
		{
			var source = fraction >> 8;
			var sample = width == 2
				? BitConverter.ToInt16( data, offset + (source * 2) )
				: (short)((data[offset + source] - 0x80) * 256);

			if ( cache.Width == 2 )
				cache.Data16[i] = sample;
			else
				cache.Data8[i] = (sbyte)((ushort)sample >> 8);

			fraction += step;
		}

		return cache;
	}

	private static sbyte SampleByte( Cache cache, int index )
	{
		if ( cache.Data8 is { } data )
			return index < data.Length ? data[index] : (sbyte)0;

		var sample = (index >> 1) < cache.Data16.Length ? cache.Data16[index >> 1] : (short)0;

		return (sbyte)((index & 1) == 0 ? sample : sample >> 8);
	}

	private static void TrimStartEndTimes( ref Word word, Cache cache, ref int channelPosition, ref int channelEnd )
	{
		word.CbTrim = cache.Length;

		float start = word.Start;
		float end = word.End;

		if ( start >= end )
			return;

		if ( start > 0f && word.Start < 100.0 )
		{
			var skip = (int)(word.Start / 100.0 * cache.Length);

			channelPosition += skip;

			var position = channelPosition;
			var scanned = 0;
			var found = false;

			while ( position < cache.Length && scanned < 255 )
			{
				if ( SampleByte( cache, position ) is > -3 and < 3 )
				{
					found = true;
					break;
				}

				position++;
				scanned++;
			}

			if ( found )
			{
				channelPosition = position;
				channelEnd -= skip + scanned;
			}

			if ( word.Pitch != 100 )
				word.SampleFrac += channelPosition << 8;
		}

		if ( end > 0f && word.End < 100.0 )
		{
			var cut = (int)((100.0 - word.End) / 100.0 * cache.Length);

			channelEnd -= cut;

			var position = cache.Length - cut;
			var scanned = 0;

			if ( channelPosition >= position )
				return;

			while ( scanned < 255 )
			{
				if ( SampleByte( cache, position ) is > -3 and < 3 )
				{
					channelEnd -= scanned;
					word.CbTrim = word.CbTrim - scanned - cut;
					return;
				}

				position--;
				scanned++;

				if ( position <= channelPosition )
					return;
			}
		}
	}

	private static void RenderWord( Word word, Cache cache, List<short> output )
	{
		var position = 0;
		var end = cache.Length;
		var time = 0;

		TrimStartEndTimes( ref word, cache, ref position, ref end );

		var pitch = ChannelPitch;

		if ( word.Pitch > 0 )
			pitch += word.Pitch - 100;

		var volume = word.Volume / 100f;

		while ( time < end )
		{
			var count = Math.Min( end - time, RenderChunk );

			if ( cache.Width == 1 && (pitch != 100 || word.TimeCompress != 0) )
			{
				var before = output.Count;
				var finished = MixPitch( ref word, cache, count, pitch, volume, ref position, ref end, output );

				time += output.Count - before;

				if ( finished || output.Count == before )
					return;

				continue;
			}

			for ( var i = 0; i < count; i++ )
			{
				var index = position + i;

				if ( cache.Width == 1 )
					output.Add( index < cache.Data8.Length ? (short)(cache.Data8[index] * 256 * volume) : (short)0 );
				else
					output.Add( index < cache.Data16.Length ? (short)(cache.Data16[index] * volume) : (short)0 );
			}

			position += count;
			time += count;
		}
	}

	private static bool MixPitch( ref Word word, Cache cache, int count, int pitch, float volume, ref int channelPosition, ref int channelEnd, List<short> output )
	{
		var data = cache.Data8;
		var step = (int)(pitch / 100f * 256f);
		var length = Math.Min( word.CbTrim, data.Length );
		var sample = word.SampleFrac >> 8;
		var fraction = word.SampleFrac + step;
		var timeCompress = word.TimeCompress;
		var mixed = 0;

		if ( step <= 0 )
			return true;

		if ( timeCompress == 0 )
		{
			while ( mixed < count && sample < length )
			{
				output.Add( (short)(data[sample] * 256 * volume) );

				mixed++;
				sample = fraction >> 8;
				fraction += step;
			}
		}
		else if ( count > 0 )
		{
			var chunk = length / 8;
			var skip = chunk * timeCompress / 100;
			var play = 0;

			while ( sample < length && chunk > 0 )
			{
				var compute = true;

				if ( sample < skip || skip <= sample % chunk )
				{
					play = chunk - (sample % chunk);
				}
				else
				{
					var scanned = 0;

					do
					{
						if ( sample >= length || scanned > 0xfe || (byte)data[sample] < 3 )
							break;

						output.Add( (short)(data[sample] * 256 * volume) );

						mixed++;
						scanned++;
						sample = fraction >> 8;
						fraction += step;
					}
					while ( mixed < count );

					if ( sample >= length || mixed >= count )
						break;

					do
					{
						if ( sample % chunk < skip )
						{
							var jump = skip - (sample % chunk);

							fraction += jump * 256;
							sample += jump;
						}

						scanned = 0;

						while ( true )
						{
							if ( sample >= length )
							{
								compute = false;
								break;
							}

							if ( scanned > 0xfe || (byte)data[sample] < 3 )
								break;

							scanned++;
							sample = fraction >> 8;
							fraction += step;
						}
					}
					while ( compute && sample % chunk < skip );

					if ( compute )
						play = chunk - (sample % chunk);
				}

				var available = (play << 8) / step;
				var run = available + mixed <= count ? available : count - mixed;

				if ( run == 0 )
					run = 1;

				var done = false;

				for ( var i = 0; i < run; i++ )
				{
					if ( sample >= length )
					{
						done = true;
						break;
					}

					output.Add( (short)(data[sample] * 256 * volume) );

					mixed++;
					sample = fraction >> 8;
					fraction += step;

					if ( mixed >= count )
					{
						done = true;
						break;
					}
				}

				if ( done )
					break;
			}
		}

		var position = channelPosition;

		word.SampleFrac = fraction - step;
		channelPosition = fraction >> 8;
		channelEnd += position - (fraction >> 8) + mixed;

		return sample >= length;
	}

	private bool ParseWordParams( ref string text, out Word word, bool first )
	{
		if ( first )
		{
			_wordDefault.Pitch = -1;
			_wordDefault.Volume = 100;
			_wordDefault.Start = 0;
			_wordDefault.End = 100;
			_wordDefault.TimeCompress = 0;
		}

		word = _wordDefault;

		if ( text.Length == 0 || text[^1] != ')' )
			return true;

		var position = 0;

		while ( text[position] != '(' )
		{
			if ( text[position] == ')' )
				return false;

			position++;
		}

		var name = text[..position];

		position++;

		while ( true )
		{
			while ( position < text.Length && text[position] is not ('v' or 'p' or 's' or 'e' or 't' or ')') )
				position++;

			if ( position >= text.Length || text[position] == ')' )
				break;

			var command = text[position++];

			if ( position >= text.Length || !char.IsAsciiDigit( text[position] ) )
				break;

			var digits = 0;
			var value = 0;

			while ( position < text.Length && char.IsAsciiDigit( text[position] ) && digits < 7 )
			{
				value = (value * 10) + (text[position] - '0');
				position++;
				digits++;
			}

			switch ( command )
			{
				case 'e':
					word.End = value;
					break;
				case 'p':
					word.Pitch = value;
					break;
				case 's':
					word.Start = value;
					break;
				case 't':
					word.TimeCompress = value;
					break;
				case 'v':
					word.Volume = value;
					break;
			}
		}

		text = name;

		if ( name.Length != 0 )
			return true;

		_wordDefault = word;

		return false;
	}

	private static List<string> ParseString( string text )
	{
		var buffer = (text + '\0').ToCharArray();
		var words = new int[MaxVoxWords];
		var period = -2;
		var comma = -3;

		Array.Fill( words, -1 );

		var position = 0;
		var count = 1;

		words[0] = 0;

		while ( count < MaxVoxWords )
		{
			var c = buffer[position];

			while ( c != '\0' && c != ' ' && c != '.' && c != ',' && c != '(' )
				c = buffer[++position];

			if ( c == '(' )
			{
				position++;

				while ( (c = buffer[position]) != '\0' )
				{
					position++;

					if ( c == ')' )
						break;
				}

				c = buffer[position];
			}

			if ( c == '\0' )
				break;

			var next = count;

			if ( c is '.' or ',' && buffer[position + 1] is not ('\n' or '\r' or '\0') )
			{
				next = count + 1;
				words[count] = c == '.' ? period : comma;

				if ( next > MaxVoxWords - 1 )
					break;
			}

			buffer[position++] = '\0';

			c = buffer[position];

			while ( c is ' ' or '.' or ',' )
				c = buffer[++position];

			if ( c == '\0' )
				break;

			words[next] = position;
			count = next + 1;
		}

		var tokens = new List<string>();

		foreach ( var word in words )
		{
			if ( word == -1 )
				break;

			if ( word == period )
			{
				tokens.Add( "_period" );
				continue;
			}

			if ( word == comma )
			{
				tokens.Add( "_comma" );
				continue;
			}

			var end = Array.IndexOf( buffer, '\0', word );

			tokens.Add( new string( buffer, word, end - word ) );
		}

		return tokens;
	}
}
