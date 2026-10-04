using System;
using System.Buffers.Binary;

namespace GoldSrc.TrueType;

public sealed class GlyphBitmap
{
	public int Left;
	public int Width;
	public int Advance;
	public int Top;
	public int Rows;
	public bool[] Pixels;
	public byte[] Levels;
}

public sealed class TrueTypeFont
{
	private const int FlagOnCurve = 0x01;
	private const int FlagXShort = 0x02;
	private const int FlagYShort = 0x04;
	private const int FlagRepeat = 0x08;
	private const int FlagXSame = 0x10;
	private const int FlagYSame = 0x20;

	private const int ComponentWords = 0x0001;
	private const int ComponentXyValues = 0x0002;
	private const int ComponentRoundXy = 0x0004;
	private const int ComponentScale = 0x0008;
	private const int ComponentMore = 0x0020;
	private const int ComponentXyScale = 0x0040;
	private const int ComponentTwoByTwo = 0x0080;
	private const int ComponentInstructions = 0x0100;
	private const int ComponentUseMyMetrics = 0x0200;
	private const int ComponentScaledOffset = 0x0800;
	private const int ComponentUnscaledOffset = 0x1000;

	private const int PhantomPoints = 8;
	private const int MaxComponentDepth = 20;
	private const int One = 0x10000;
	private const int PointsPerInch = 72;
	private const int LogicalDpi = 96;

	private readonly byte[] _data;
	private readonly Dictionary<string, (int Offset, int Length)> _tables = [];
	private readonly Dictionary<int, int> _deviceRecords = [];
	private static readonly ushort[] AnsiCodePage =
	[
		0x20AC, 0x0081, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x008D, 0x017D, 0x008F,
		0x0090, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x009D, 0x017E, 0x0178
	];

	private readonly ushort[] _characterMap = new ushort[0x10000];

	private readonly int _horizontalMetricCount;
	private readonly bool _longLocations;
	private readonly int _winAscent;
	private readonly int _winDescent;

	private readonly Interpreter _interpreter;

	private int _pixelsPerEm;
	private FontScale _scale;
	private FontScale _childScaleX;
	private FontScale _childScaleY;
	private bool _identityScale = true;
	private int _glyphTop;
	private bool _fontProgramPending = true;
	private bool _hinted;
	private bool _gridFit;

	public int UnitsPerEm { get; }

	public int GlyphCount { get; }

	public int MaxPoints { get; }

	public int MaxContours { get; }

	public int MaxTwilightPoints { get; }

	public int MaxStorage { get; }

	public int MaxFunctionDefs { get; }

	public int MaxInstructionDefs { get; }

	public int MaxStackElements { get; }

	public TrueTypeFont( byte[] data )
	{
		_data = data;

		var tableCount = UInt16( 4 );

		for ( var i = 0; i < tableCount; i++ )
		{
			var record = 12 + (i * 16);

			_tables[System.Text.Encoding.ASCII.GetString( _data, record, 4 )] = ((int)UInt32( record + 8 ), (int)UInt32( record + 12 ));
		}

		var head = _tables["head"].Offset;
		var maxp = _tables["maxp"].Offset;
		var os2 = _tables["OS/2"].Offset;

		UnitsPerEm = UInt16( head + 18 );
		_longLocations = Int16( head + 50 ) != 0;
		_horizontalMetricCount = UInt16( _tables["hhea"].Offset + 34 );
		_winAscent = UInt16( os2 + 74 );
		_winDescent = UInt16( os2 + 76 );

		GlyphCount = UInt16( maxp + 4 );
		MaxPoints = Math.Max( UInt16( maxp + 6 ), UInt16( maxp + 10 ) );
		MaxContours = Math.Max( UInt16( maxp + 8 ), UInt16( maxp + 12 ) );
		MaxTwilightPoints = UInt16( maxp + 16 );
		MaxStorage = UInt16( maxp + 18 );
		MaxFunctionDefs = UInt16( maxp + 20 );
		MaxInstructionDefs = UInt16( maxp + 22 );
		MaxStackElements = UInt16( maxp + 24 );

		ReadCharacterMap();
		ReadDeviceMetrics();

		_interpreter = new Interpreter( this );
	}

	private ushort UInt16( int offset ) => BinaryPrimitives.ReadUInt16BigEndian( _data.AsSpan( offset ) );

	private short Int16( int offset ) => BinaryPrimitives.ReadInt16BigEndian( _data.AsSpan( offset ) );

	private uint UInt32( int offset ) => BinaryPrimitives.ReadUInt32BigEndian( _data.AsSpan( offset ) );

	private void ReadCharacterMap()
	{
		var cmap = _tables["cmap"].Offset;
		var count = UInt16( cmap + 2 );

		for ( var i = 0; i < count; i++ )
		{
			var record = cmap + 4 + (i * 8);

			if ( UInt16( record ) != 3 || UInt16( record + 2 ) != 1 )
				continue;

			var subtable = cmap + (int)UInt32( record + 4 );

			if ( UInt16( subtable ) != 4 )
				continue;

			var segmentBytes = UInt16( subtable + 6 );
			var segments = segmentBytes / 2;
			var ends = subtable + 14;
			var starts = ends + segmentBytes + 2;
			var deltas = starts + segmentBytes;
			var rangeOffsets = deltas + segmentBytes;

			for ( var segment = 0; segment < segments; segment++ )
			{
				var start = UInt16( starts + (segment * 2) );
				var end = UInt16( ends + (segment * 2) );
				var delta = Int16( deltas + (segment * 2) );
				var rangeOffset = UInt16( rangeOffsets + (segment * 2) );

				for ( var character = (int)start; character <= end && character < _characterMap.Length; character++ )
				{
					if ( rangeOffset == 0 )
					{
						_characterMap[character] = (ushort)(character + delta);
						continue;
					}

					var glyph = UInt16( rangeOffsets + (segment * 2) + rangeOffset + ((character - start) * 2) );

					_characterMap[character] = glyph == 0 ? (ushort)0 : (ushort)(glyph + delta);
				}
			}

			return;
		}
	}

	private void ReadDeviceMetrics()
	{
		if ( !_tables.TryGetValue( "hdmx", out var hdmx ) )
			return;

		var count = Int16( hdmx.Offset + 2 );
		var size = (int)UInt32( hdmx.Offset + 4 );

		for ( var i = 0; i < count; i++ )
		{
			var record = hdmx.Offset + 8 + (i * size);

			_deviceRecords[_data[record]] = record + 2;
		}
	}

	public void SelectHeight( int tall, out int pixelsPerEm, out int ascent, out int height )
	{
		if ( SearchVdmx( tall, out pixelsPerEm, out var top, out var bottom ) )
		{
			ascent = top;
			height = top - bottom;
			return;
		}

		var cellUnits = _winAscent + _winDescent;
		var metrics = false;
		var incremented = false;
		var decremented = false;
		var previousFound = false;
		var previousTop = 0;
		var previousBottom = 0;

		var scale = Fixed.FixDiv( tall, cellUnits );

		pixelsPerEm = Fixed.FixMul( scale, UnitsPerEm );

		if ( !_tables.ContainsKey( "VDMX" ) )
		{
			pixelsPerEm = ((scale * UnitsPerEm) + 0x8000) >> 16;
			ascent = Fixed.FixMul( _winAscent, scale );
			height = ascent + Fixed.FixMul( _winDescent, scale );
			return;
		}

		for ( var attempt = 0; attempt <= 0xFF; attempt++ )
		{
			var found = SearchVdmx( -pixelsPerEm, out _, out top, out bottom );
			var cell = found ? top - bottom : Fixed.RoundedDivide( (long)cellUnits * pixelsPerEm, UnitsPerEm );

			if ( cell == tall )
			{
				metrics = found;
				break;
			}

			if ( cell < tall )
			{
				if ( decremented )
				{
					metrics = found;
					break;
				}

				pixelsPerEm++;
				incremented = true;
			}
			else
			{
				pixelsPerEm--;

				if ( incremented )
				{
					metrics = previousFound;
					top = previousTop;
					bottom = previousBottom;
					break;
				}

				decremented = true;
			}

			previousFound = found;
			previousTop = top;
			previousBottom = bottom;
		}

		if ( metrics )
		{
			ascent = top;
			height = top - bottom;
			return;
		}

		ascent = Fixed.FixMul( _winAscent, scale );
		height = ascent + Fixed.FixMul( _winDescent, scale );
	}

	private bool SearchVdmx( int request, out int pixelsPerEm, out int top, out int bottom )
	{
		pixelsPerEm = 0;
		top = 0;
		bottom = 0;

		if ( !_tables.TryGetValue( "VDMX", out var vdmx ) || vdmx.Length <= 5 || request is < -0xFE or > 0xFE )
			return false;

		var ratios = UInt16( vdmx.Offset + 4 );
		var ratio = 0;

		for ( ; ratio < ratios; ratio++ )
		{
			var entry = vdmx.Offset + 6 + (ratio * 4);

			if ( _data[entry] != 1 )
				continue;

			var xRatio = _data[entry + 1];

			if ( xRatio == 0 || (_data[entry + 2] <= xRatio && xRatio <= _data[entry + 3]) )
				break;
		}

		if ( ratio == ratios )
			return false;

		var group = vdmx.Offset + UInt16( vdmx.Offset + 6 + (ratios * 4) + (ratio * 2) );
		var records = UInt16( group );

		if ( request <= 0 && (-request < _data[group + 2] || -request > _data[group + 3]) )
			return false;

		for ( var i = 0; i < records; i++ )
		{
			var record = group + 4 + (i * 6);

			pixelsPerEm = UInt16( record );
			top = Int16( record + 2 );
			bottom = Int16( record + 4 );

			if ( request <= 0 )
			{
				if ( pixelsPerEm == -request )
					return true;

				if ( -request <= pixelsPerEm )
					return false;

				continue;
			}

			var cell = top - bottom;

			if ( cell > request )
				return false;

			if ( cell == request )
				return pixelsPerEm <= 0xFF;
		}

		return false;
	}

	public void SetSize( int pixelsPerEm )
	{
		var pointSize = ((pixelsPerEm * PointsPerInch) + (LogicalDpi / 2)) / LogicalDpi;

		_emboldenSteps = ((EmboldenWeight * pixelsPerEm) - 10) / 1000;

		if ( Embolden && pixelsPerEm > EmboldenAdjustSize )
			pixelsPerEm -= (pixelsPerEm * EmboldenAdjustFactor) >> 16;

		_pixelsPerEm = pixelsPerEm;
		_scale = new FontScale( pixelsPerEm << 16, UnitsPerEm << 16 );

		var values = Array.Empty<int>();

		if ( _tables.TryGetValue( "cvt ", out var cvt ) )
		{
			values = new int[cvt.Length / 2];

			for ( var i = 0; i < values.Length; i++ )
				values[i] = _scale.Apply( Int16( cvt.Offset + (i * 2) ) );
		}

		_interpreter.SetSize( pixelsPerEm, pointSize, _scale, values );
		_hinted = true;

		if ( _fontProgramPending )
		{
			_fontProgramPending = false;

			if ( _tables.TryGetValue( "fpgm", out var fontProgram ) )
			{
				_interpreter.RunFontProgram( _data, fontProgram.Offset, fontProgram.Length );
				_hinted = !_interpreter.Failed;
			}
		}

		if ( _hinted )
		{
			_tables.TryGetValue( "prep", out var program );
			_interpreter.RunControlProgram( _data, program.Offset, program.Length );
			_hinted = !_interpreter.Failed;
		}
	}

	public int Scale( int value ) => _scale.Apply( value );

	private int GlyphOffset( int glyph, out int length )
	{
		var loca = _tables["loca"].Offset;
		int start;
		int end;

		if ( _longLocations )
		{
			start = (int)UInt32( loca + (glyph * 4) );
			end = (int)UInt32( loca + (glyph * 4) + 4 );
		}
		else
		{
			start = UInt16( loca + (glyph * 2) ) * 2;
			end = UInt16( loca + (glyph * 2) + 2 ) * 2;
		}

		length = end - start;

		return _tables["glyf"].Offset + start;
	}

	private void HorizontalMetrics( int glyph, out int advance, out int leftSideBearing )
	{
		var hmtx = _tables["hmtx"].Offset;

		if ( glyph < _horizontalMetricCount )
		{
			advance = UInt16( hmtx + (glyph * 4) );
			leftSideBearing = Int16( hmtx + (glyph * 4) + 2 );
			return;
		}

		advance = UInt16( hmtx + ((_horizontalMetricCount - 1) * 4) );
		leftSideBearing = Int16( hmtx + (_horizontalMetricCount * 4) + ((glyph - _horizontalMetricCount) * 2) );
	}

	private void VerticalMetrics( int glyph, out int advance, out int topSideBearing )
	{
		if ( _tables.TryGetValue( "vhea", out var vhea ) && _tables.TryGetValue( "vmtx", out var vmtx ) && vmtx.Length != 0 )
		{
			var count = UInt16( vhea.Offset + 34 );

			if ( glyph < count )
			{
				advance = UInt16( vmtx.Offset + (glyph * 4) );
				topSideBearing = Int16( vmtx.Offset + (glyph * 4) + 2 );
				return;
			}

			advance = UInt16( vmtx.Offset + ((count - 1) * 4) );
			topSideBearing = Int16( vmtx.Offset + (count * 4) + ((glyph - count) * 2) );
			return;
		}

		var hhea = _tables["hhea"].Offset;
		var ascender = Int16( hhea + 4 );
		var descender = Int16( hhea + 6 );

		if ( _tables.TryGetValue( "OS/2", out var os2 ) && os2.Length != 0 )
		{
			ascender = Int16( os2.Offset + 68 );
			descender = Int16( os2.Offset + 70 );
		}

		advance = (ushort)(ascender - descender);
		topSideBearing = (short)(ascender - _glyphTop);
	}

	private sealed class GlyphException : Exception;

	private sealed class GlyphRecord
	{
		public GlyphRecord Parent;
		public int Glyph;
		public bool OffsetArguments;
		public bool RoundOffset;
		public bool UseMyMetrics;
		public bool ScaledOffset;
		public int OffsetX;
		public int OffsetY;
		public int ParentPoint;
		public int ChildPoint;
		public int[] Matrix = [One, 0, 0, One];
		public int[] Transform = [One, 0, 0, One];
		public bool Identity = true;
		public bool MetricsSet;
		public int MetricsLeftX;
		public int MetricsLeftY;
		public int MetricsRightX;
		public int MetricsRightY;
		public int ScanKind = 0xFFFF;
		public List<int> X = [];
		public List<int> Y = [];
		public List<bool> OnCurve = [];
		public List<int> ContourEnds = [];
		public int[] PhantomX = new int[PhantomPoints];
		public int[] PhantomY = new int[PhantomPoints];
	}

	private GlyphRecord LoadOutline( int glyph )
	{
		var offset = GlyphOffset( glyph, out var length );

		_glyphTop = length == 0 ? 0 : Int16( offset + 8 );
		_gridFit = _hinted;

		while ( true )
		{
			var record = new GlyphRecord { Glyph = glyph, Transform = [_pixelsPerEm << 16, 0, 0, _pixelsPerEm << 16] };

			_interpreter.ResetBudget();

			try
			{
				LoadGlyph( record, 0 );
			}
			catch ( GlyphException ) when ( _gridFit )
			{
				_gridFit = false;
				continue;
			}

			var shiftX = _gridFit ? Fixed.PixRound( -record.PhantomX[0] ) : -record.PhantomX[0];
			var shiftY = -record.PhantomY[0];

			for ( var i = 0; i < record.X.Count; i++ )
			{
				record.X[i] += shiftX;
				record.Y[i] += shiftY;
			}

			for ( var i = 0; i < PhantomPoints; i++ )
			{
				record.PhantomX[i] += shiftX;
				record.PhantomY[i] += shiftY;
			}

			return record;
		}
	}

	private void LoadGlyph( GlyphRecord record, int depth )
	{
		var offset = GlyphOffset( record.Glyph, out var length );
		var contourCount = 1;
		var left = 0;
		var top = 0;

		if ( length != 0 )
		{
			if ( length < 10 )
				throw new GlyphException();

			contourCount = Int16( offset );
			left = Int16( offset + 2 );
			top = Int16( offset + 8 );

			if ( contourCount < -1 || left > Int16( offset + 6 ) || Int16( offset + 4 ) > top )
				throw new GlyphException();
		}

		HorizontalMetrics( record.Glyph, out var advanceWidth, out var leftSideBearing );
		VerticalMetrics( record.Glyph, out var advanceHeight, out var topSideBearing );

		var zone = _interpreter.GlyphZone;

		if ( contourCount == -1 )
		{
			var read = LoadComponents( record, offset + 10, depth, out var instructed );
			var instructionLength = instructed ? UInt16( read ) : 0;
			var count = record.X.Count;

			zone.Reset( count + PhantomPoints, record.ContourEnds.Count );

			for ( var i = 0; i < count; i++ )
			{
				zone.CurX[i] = record.X[i];
				zone.CurY[i] = record.Y[i];
				zone.Tags[i] = record.OnCurve[i] ? Zone.OnCurve : (byte)0;
			}

			for ( var i = 0; i < record.ContourEnds.Count; i++ )
				zone.Contours[i] = record.ContourEnds[i];

			SetOriginalPhantoms( zone, count, left, top, leftSideBearing, topSideBearing, advanceWidth, advanceHeight );

			if ( GridFitComposite( record, zone, count, read + 2, instructionLength ) )
				record.ScanKind = _interpreter.ScanKind;

			StoreZone( record, zone, count );

			if ( record.MetricsSet )
			{
				record.PhantomX[0] = record.MetricsLeftX;
				record.PhantomY[0] = record.MetricsLeftY;
				record.PhantomX[1] = record.MetricsRightX;
				record.PhantomY[1] = record.MetricsRightY;
			}
		}
		else
		{
			var count = ReadOutline( zone, offset, length != 0, contourCount, out var instructions, out var instructionLength );

			SetOriginalPhantoms( zone, count, left, top, leftSideBearing, topSideBearing, advanceWidth, advanceHeight );
			GridFit( record, zone, count, instructions, instructionLength );

			record.ScanKind = _interpreter.ScanKind;

			StoreZone( record, zone, count );
		}

		if ( record.Parent is not null )
			Attach( record );
	}

	private int LoadComponents( GlyphRecord record, int read, int depth, out bool instructed )
	{
		if ( depth + 1 > MaxComponentDepth )
			throw new GlyphException();

		var children = new List<GlyphRecord>();
		int flags;

		instructed = false;

		do
		{
			flags = UInt16( read );

			var child = new GlyphRecord
			{
				Parent = record,
				Glyph = UInt16( read + 2 ),
				Transform = [.. record.Transform],
				Identity = record.Identity,
				OffsetArguments = (flags & ComponentXyValues) != 0,
				RoundOffset = (flags & ComponentRoundXy) != 0,
				UseMyMetrics = (flags & ComponentUseMyMetrics) != 0,
				ScaledOffset = (flags & ComponentScaledOffset) != 0 && (flags & ComponentUnscaledOffset) == 0
			};

			instructed |= (flags & ComponentInstructions) != 0;
			read += 4;

			if ( (flags & ComponentWords) != 0 )
			{
				if ( child.OffsetArguments )
				{
					child.OffsetX = Int16( read );
					child.OffsetY = Int16( read + 2 );
				}
				else
				{
					child.ParentPoint = UInt16( read );
					child.ChildPoint = UInt16( read + 2 );
				}

				read += 4;
			}
			else
			{
				if ( child.OffsetArguments )
				{
					child.OffsetX = (sbyte)_data[read];
					child.OffsetY = (sbyte)_data[read + 1];
				}
				else
				{
					child.ParentPoint = _data[read];
					child.ChildPoint = _data[read + 1];
				}

				read += 2;
			}

			if ( (flags & (ComponentScale | ComponentXyScale | ComponentTwoByTwo)) != 0 )
			{
				var matrix = child.Matrix;

				if ( (flags & ComponentTwoByTwo) != 0 )
				{
					matrix[0] = Int16( read ) * 4;
					matrix[1] = Int16( read + 2 ) * 4;
					matrix[2] = Int16( read + 4 ) * 4;
					matrix[3] = Int16( read + 6 ) * 4;
					read += 8;
				}
				else
				{
					matrix[0] = matrix[3] = Int16( read ) << 2;
					read += 2;

					if ( (flags & ComponentXyScale) != 0 )
					{
						matrix[3] = Int16( read ) * 4;
						read += 2;
					}
				}

				var transform = child.Transform;
				var xx = Fixed.FixMul( matrix[1], transform[2] ) + Fixed.FixMul( matrix[0], transform[0] );
				var xy = Fixed.FixMul( matrix[1], transform[3] ) + Fixed.FixMul( matrix[0], transform[1] );
				var yx = Fixed.FixMul( matrix[3], transform[2] ) + Fixed.FixMul( matrix[2], transform[0] );
				var yy = Fixed.FixMul( matrix[3], transform[3] ) + Fixed.FixMul( matrix[2], transform[1] );

				transform[0] = xx;
				transform[1] = xy;
				transform[2] = yx;
				transform[3] = yy;

				if ( matrix[1] != 0 || matrix[2] != 0 || Math.Abs( matrix[0] ) != Math.Abs( matrix[3] ) || Math.Abs( matrix[0] ) != One )
					child.Identity = false;
			}

			if ( child.Glyph >= GlyphCount )
				throw new GlyphException();

			children.Add( child );
		}
		while ( (flags & ComponentMore) != 0 );

		foreach ( var child in children )
			LoadGlyph( child, depth + 1 );

		return read;
	}

	private int ReadOutline( Zone zone, int offset, bool hasOutline, int contourCount, out int instructions, out int instructionLength )
	{
		instructions = 0;
		instructionLength = 0;

		if ( !hasOutline )
		{
			zone.Reset( 1 + PhantomPoints, 1 );
			zone.Contours[0] = 0;
			zone.Tags[0] = Zone.OnCurve;

			return 1;
		}

		var maxp = _tables["maxp"].Offset;

		if ( contourCount < 1 || contourCount > UInt16( maxp + 8 ) )
			throw new GlyphException();

		var read = offset + 10;
		var pointCount = Int16( read + ((contourCount - 1) * 2) ) + 1;

		if ( pointCount < 1 || pointCount > UInt16( maxp + 6 ) )
			throw new GlyphException();

		zone.Reset( pointCount + PhantomPoints, contourCount );

		for ( var i = 0; i < contourCount; i++ )
		{
			zone.Contours[i] = Int16( read );
			read += 2;

			if ( zone.Contours[i] < (i == 0 ? 0 : zone.Contours[i - 1] + 1) )
				throw new GlyphException();
		}

		instructionLength = UInt16( read );
		instructions = read + 2;
		read = instructions + instructionLength;

		var flags = new byte[pointCount];

		for ( var i = 0; i < pointCount; i++ )
		{
			var flag = _data[read++];

			flags[i] = flag;

			if ( (flag & FlagRepeat) == 0 )
				continue;

			for ( var repeat = _data[read++]; repeat > 0 && i + 1 < pointCount; repeat-- )
				flags[++i] = flag;
		}

		short value = 0;

		for ( var i = 0; i < pointCount; i++ )
		{
			if ( (flags[i] & FlagXShort) != 0 )
			{
				var delta = _data[read++];

				value += (short)((flags[i] & FlagXSame) != 0 ? delta : -delta);
			}
			else if ( (flags[i] & FlagXSame) == 0 )
			{
				value += Int16( read );
				read += 2;
			}

			zone.OrusX[i] = value;
		}

		value = 0;

		for ( var i = 0; i < pointCount; i++ )
		{
			if ( (flags[i] & FlagYShort) != 0 )
			{
				var delta = _data[read++];

				value += (short)((flags[i] & FlagYSame) != 0 ? delta : -delta);
			}
			else if ( (flags[i] & FlagYSame) == 0 )
			{
				value += Int16( read );
				read += 2;
			}

			zone.OrusY[i] = value;
			zone.Tags[i] = (byte)(flags[i] & FlagOnCurve);
		}

		return pointCount;
	}

	private void SetOriginalPhantoms( Zone zone, int count, int left, int top, int leftSideBearing, int topSideBearing, int advanceWidth, int advanceHeight )
	{
		var x = zone.OrusX;
		var y = zone.OrusY;
		Array.Clear( x, count, PhantomPoints );
		Array.Clear( y, count, PhantomPoints );

		x[count] = left - leftSideBearing;
		x[count + 1] = x[count] + advanceWidth;
		x[count + 4] = x[count];
		x[count + 5] = left;

		y[count + 2] = top + topSideBearing;
		y[count + 3] = y[count + 2] - advanceHeight;
		y[count + 6] = y[count + 2];
		y[count + 7] = top;
	}

	private void InitializeChildScaling( int[] transform )
	{
		_childScaleX = new FontScale( Math.Max( Math.Abs( transform[0] ), Math.Abs( transform[1] ) ), UnitsPerEm << 16 );
		_childScaleY = new FontScale( Math.Max( Math.Abs( transform[2] ), Math.Abs( transform[3] ) ), UnitsPerEm << 16 );
	}

	private void ScaleOriginal( Zone zone, int start, int count )
	{
		var scaleX = _identityScale ? _scale : _childScaleX;
		var scaleY = _identityScale ? _scale : _childScaleY;

		for ( var i = start; i < start + count; i++ )
		{
			zone.OrgX[i] = scaleX.Apply( zone.OrusX[i] );
			zone.OrgY[i] = scaleY.Apply( zone.OrusY[i] );
		}
	}

	private void ScaleBack( Zone zone, int count )
	{
		var scaleX = _identityScale ? _scale : _childScaleX;
		var scaleY = _identityScale ? _scale : _childScaleY;

		for ( var i = 0; i < count; i++ )
		{
			zone.CurX[i] = scaleX.Reverse( zone.CurX[i] );
			zone.CurY[i] = scaleY.Reverse( zone.CurY[i] );
		}
	}

	private int ScaleMetric( int value )
	{
		var product = (long)value * (_pixelsPerEm << 16);

		return ((int)((product + (UnitsPerEm / 2)) / UnitsPerEm) + 0x200) >> 10;
	}

	private void RoundSideBearings( Zone zone, int count )
	{
		var width = (short)(zone.OrusX[count + 1] - zone.OrusX[count]);
		var height = (short)(zone.OrusY[count + 3] - zone.OrusY[count + 2]);

		zone.CurX[count + 1] = Fixed.PixRound( ScaleMetric( width ) ) + zone.CurX[count];
		zone.CurY[count + 2] = Fixed.PixRound( zone.CurY[count + 2] );
		zone.CurY[count + 3] = Fixed.PixRound( ScaleMetric( height ) + zone.CurY[count + 2] );
	}

	private void RunProgram( Zone zone, int count, int instructions, int instructionLength, bool scaledOriginals )
	{
		for ( var i = 0; i < count + PhantomPoints; i++ )
			zone.Tags[i] &= Zone.OnCurve;

		if ( scaledOriginals )
		{
			Array.Copy( zone.OrgX, zone.OrusX, count + PhantomPoints );
			Array.Copy( zone.OrgY, zone.OrusY, count + PhantomPoints );
		}

		_interpreter.RunGlyphProgram( _data, instructions, instructionLength, scaledOriginals );

		if ( _interpreter.Failed )
			throw new GlyphException();
	}

	private void GridFit( GlyphRecord record, Zone zone, int count, int instructions, int instructionLength )
	{
		var total = count + PhantomPoints;

		_interpreter.ResetGlyphState();

		if ( _interpreter.GridFitInhibited || !_gridFit )
		{
			if ( record.Identity )
			{
				ScaleOriginal( zone, 0, total );
				Array.Copy( zone.OrgX, zone.CurX, total );
				Array.Copy( zone.OrgY, zone.CurY, total );
				return;
			}

			for ( var i = 0; i < total; i++ )
			{
				zone.CurX[i] = zone.OrusX[i] << 6;
				zone.CurY[i] = zone.OrusY[i] << 6;
			}

			return;
		}

		_identityScale = record.Identity;

		if ( !record.Identity )
			InitializeChildScaling( record.Transform );

		ScaleOriginal( zone, 0, total );

		var shift = Fixed.PixRound( zone.OrgX[count] ) - zone.OrgX[count];

		if ( shift != 0 )
		{
			for ( var i = 0; i < total; i++ )
				zone.OrgX[i] += shift;
		}

		Array.Copy( zone.OrgX, zone.CurX, total );
		Array.Copy( zone.OrgY, zone.CurY, total );

		RoundSideBearings( zone, count );

		if ( instructionLength != 0 )
			RunProgram( zone, count, instructions, instructionLength, !record.Identity );

		if ( !record.Identity )
			ScaleBack( zone, total );
	}

	private bool GridFitComposite( GlyphRecord record, Zone zone, int count, int instructions, int instructionLength )
	{
		var changed = false;

		_interpreter.ResetGlyphState();

		Array.Copy( zone.CurX, zone.OrgX, count );
		Array.Copy( zone.CurY, zone.OrgY, count );

		if ( _interpreter.GridFitInhibited || !_gridFit )
		{
			if ( record.Identity )
			{
				ScaleOriginal( zone, count, PhantomPoints );
				Array.Copy( zone.OrgX, count, zone.CurX, count, PhantomPoints );
				Array.Copy( zone.OrgY, count, zone.CurY, count, PhantomPoints );
				return false;
			}

			for ( var i = count; i < count + PhantomPoints; i++ )
			{
				zone.CurX[i] = zone.OrusX[i] << 6;
				zone.CurY[i] = zone.OrusY[i] << 6;
			}

			return false;
		}

		_identityScale = record.Identity;

		if ( !record.Identity )
		{
			InitializeChildScaling( record.Transform );

			for ( var i = 0; i < count; i++ )
			{
				zone.CurX[i] = _childScaleX.ApplyFixed( zone.CurX[i] );
				zone.CurY[i] = _childScaleY.ApplyFixed( zone.CurY[i] );
			}
		}

		ScaleOriginal( zone, count, PhantomPoints );

		var left = zone.OrgX[count];

		zone.OrgX[count] = Fixed.PixRound( left );
		zone.OrgX[count + 1] += zone.OrgX[count] - left;

		Array.Copy( zone.OrgX, count, zone.CurX, count, PhantomPoints );
		Array.Copy( zone.OrgY, count, zone.CurY, count, PhantomPoints );

		RoundSideBearings( zone, count );

		if ( instructionLength != 0 )
		{
			RunProgram( zone, count, instructions, instructionLength, true );
			changed = _interpreter.ScanStateChanged;
		}

		if ( !record.Identity )
			ScaleBack( zone, count + PhantomPoints );

		return changed;
	}

	private static void StoreZone( GlyphRecord record, Zone zone, int count )
	{
		record.X.Clear();
		record.Y.Clear();
		record.OnCurve.Clear();
		record.ContourEnds.Clear();

		for ( var i = 0; i < count; i++ )
		{
			record.X.Add( zone.CurX[i] );
			record.Y.Add( zone.CurY[i] );
			record.OnCurve.Add( (zone.Tags[i] & Zone.OnCurve) != 0 );
		}

		for ( var i = 0; i < zone.ContourCount; i++ )
			record.ContourEnds.Add( zone.Contours[i] );

		Array.Copy( zone.CurX, count, record.PhantomX, 0, PhantomPoints );
		Array.Copy( zone.CurY, count, record.PhantomY, 0, PhantomPoints );
	}

	private static void ChildPoint( GlyphRecord child, int index, out int x, out int y )
	{
		var count = child.X.Count;

		if ( index < count )
		{
			x = child.X[index];
			y = child.Y[index];
			return;
		}

		x = child.PhantomX[index - count];
		y = child.PhantomY[index - count];
	}

	private void Attach( GlyphRecord child )
	{
		var parent = child.Parent;
		var matrix = child.Matrix;
		var count = child.X.Count;

		if ( matrix[0] != One || matrix[3] != One || matrix[1] != 0 || matrix[2] != 0 )
		{
			for ( var i = 0; i < count; i++ )
			{
				var x = child.X[i];
				var y = child.Y[i];

				child.X[i] = Fixed.FixMul( matrix[2], y ) + Fixed.FixMul( matrix[0], x );
				child.Y[i] = Fixed.FixMul( matrix[3], y ) + Fixed.FixMul( matrix[1], x );
			}

			for ( var i = 0; i < PhantomPoints; i++ )
			{
				var x = child.PhantomX[i];
				var y = child.PhantomY[i];

				child.PhantomX[i] = Fixed.FixMul( matrix[2], y ) + Fixed.FixMul( matrix[0], x );
				child.PhantomY[i] = Fixed.FixMul( matrix[3], y ) + Fixed.FixMul( matrix[1], x );
			}
		}

		if ( !child.Identity && parent.Identity )
		{
			_identityScale = true;

			for ( var i = 0; i < count; i++ )
			{
				child.X[i] = _scale.ApplyFixed( child.X[i] );
				child.Y[i] = _scale.ApplyFixed( child.Y[i] );
			}

			for ( var i = 0; i < PhantomPoints; i++ )
			{
				child.PhantomX[i] = _scale.ApplyFixed( child.PhantomX[i] );
				child.PhantomY[i] = _scale.ApplyFixed( child.PhantomY[i] );
			}

			child.Identity = true;
		}

		int shiftX;
		int shiftY;

		if ( child.OffsetArguments )
		{
			if ( !parent.Identity )
				InitializeChildScaling( parent.Transform );

			if ( child.Identity )
			{
				shiftX = _scale.Apply( child.OffsetX );
				shiftY = _scale.Apply( child.OffsetY );
			}
			else
			{
				shiftX = _childScaleX.Apply( child.OffsetX );
				shiftY = _childScaleY.Apply( child.OffsetY );
			}

			if ( child.ScaledOffset )
			{
				var scaleX = Math.Max( Math.Abs( matrix[0] ), Math.Abs( matrix[1] ) );
				var scaleY = Math.Max( Math.Abs( matrix[2] ), Math.Abs( matrix[3] ) );

				if ( scaleX != One || scaleY != One )
				{
					shiftX = Fixed.FixMul( shiftX, scaleX );
					shiftY = Fixed.FixMul( shiftY, scaleY );
				}
			}

			if ( child.RoundOffset )
			{
				shiftX = Fixed.PixRound( shiftX );
				shiftY = Fixed.PixRound( shiftY );
			}

			if ( !child.Identity )
			{
				shiftX = _childScaleX.Reverse( shiftX );
				shiftY = _childScaleY.Reverse( shiftY );
			}
		}
		else
		{
			var parentCount = parent.X.Count;

			if ( parent.ContourEnds.Count == 0 || child.ParentPoint > parentCount - 1 + PhantomPoints || child.ChildPoint > count - 1 + PhantomPoints )
				throw new GlyphException();

			ChildPoint( child, child.ChildPoint, out var childX, out var childY );

			int parentX;
			int parentY;

			if ( child.ParentPoint < parentCount )
			{
				parentX = parent.X[child.ParentPoint];
				parentY = parent.Y[child.ParentPoint];
			}
			else
			{
				ChildPoint( child, child.ParentPoint - parentCount, out parentX, out parentY );
			}

			shiftX = parentX - childX;
			shiftY = parentY - childY;
		}

		if ( child.UseMyMetrics )
		{
			parent.MetricsSet = true;
			parent.MetricsLeftX = child.PhantomX[0];
			parent.MetricsLeftY = child.PhantomY[0];
			parent.MetricsRightX = child.PhantomX[1];
			parent.MetricsRightY = child.PhantomY[1];
		}

		parent.ScanKind = parent.ScanKind == 0xFFFF ? child.ScanKind : ((child.ScanKind & 3) | 4) & parent.ScanKind;

		var first = parent.X.Count;

		for ( var i = 0; i < count; i++ )
		{
			parent.X.Add( child.X[i] + shiftX );
			parent.Y.Add( child.Y[i] + shiftY );
			parent.OnCurve.Add( child.OnCurve[i] );
		}

		foreach ( var end in child.ContourEnds )
			parent.ContourEnds.Add( end + first );
	}

	private const int GrayShift = 3;

	private const int EmboldenWeight = 20;
	private const int EmboldenAdjustSize = 50;
	private const int EmboldenAdjustFactor = 0x51E;

	private int _emboldenSteps;

	public bool Embolden { get; set; }

	public bool Grayscale
	{
		get => _interpreter.Grayscale;
		set => _interpreter.Grayscale = value;
	}

	public GlyphBitmap Render( char character ) => Render( _characterMap[character] );

	public GlyphBitmap RenderAnsi( byte character )
	{
		return Render( _characterMap[character is >= 0x80 and <= 0x9F ? AnsiCodePage[character - 0x80] : character] );
	}

	private GlyphBitmap Emboldened( GlyphBitmap bitmap )
	{
		var bold = new GlyphBitmap
		{
			Advance = bitmap.Advance + 1,
			Left = bitmap.Left,
			Width = bitmap.Width,
			Top = bitmap.Top,
			Rows = bitmap.Rows
		};

		if ( bitmap.Width == 0 || bitmap.Rows == 0 )
		{
			bold.Pixels = bitmap.Pixels;
			return bold;
		}

		var right = _emboldenSteps + 1;
		var up = _emboldenSteps;

		bold.Width += right;
		bold.Top += up;
		bold.Rows += up;
		bold.Pixels = new bool[bold.Width * bold.Rows];

		for ( var row = 0; row < bitmap.Rows; row++ )
		{
			for ( var column = 0; column < bitmap.Width; column++ )
			{
				if ( !bitmap.Pixels[(row * bitmap.Width) + column] )
					continue;

				for ( var y = 0; y <= up; y++ )
				{
					for ( var x = 0; x <= right; x++ )
						bold.Pixels[((row + y) * bold.Width) + column + x] = true;
				}
			}
		}

		return bold;
	}

	private GlyphBitmap Render( int glyph )
	{
		var outline = LoadOutline( glyph );
		var advance = Fixed.PixRound( outline.PhantomX[1] - outline.PhantomX[0] ) >> 6;

		if ( _deviceRecords.TryGetValue( _pixelsPerEm, out var record ) )
			advance = _data[record + glyph];

		if ( !Grayscale )
		{
			var bitmap = Rasterizer.Render( outline.X, outline.Y, outline.OnCurve, outline.ContourEnds, advance, outline.ScanKind );

			if ( glyph == 0 )
				Array.Clear( bitmap.Pixels );

			return Embolden ? Emboldened( bitmap ) : bitmap;
		}

		for ( var i = 0; i < outline.X.Count; i++ )
		{
			outline.X[i] <<= GrayShift;
			outline.Y[i] <<= GrayShift;
		}

		var fine = Rasterizer.Render( outline.X, outline.Y, outline.OnCurve, outline.ContourEnds, advance, outline.ScanKind );
		var scale = 1 << GrayShift;
		var left = fine.Left >> GrayShift;
		var right = (fine.Left + fine.Width + scale - 1) >> GrayShift;
		var bottom = (fine.Top - fine.Rows) >> GrayShift;
		var top = (fine.Top + scale - 1) >> GrayShift;

		var gray = new GlyphBitmap
		{
			Advance = advance,
			Left = left,
			Width = right - left,
			Top = top,
			Rows = top - bottom
		};

		gray.Levels = new byte[gray.Width * gray.Rows];

		for ( var row = 0; row < fine.Rows; row++ )
		{
			var y = (top - 1) - ((fine.Top - 1 - row) >> GrayShift);

			for ( var column = 0; column < fine.Width; column++ )
			{
				if ( fine.Pixels[(row * fine.Width) + column] )
					gray.Levels[(y * gray.Width) + ((fine.Left + column) >> GrayShift) - left]++;
			}
		}

		return gray;
	}
}
