using System;

namespace GoldSrc.TrueType;

public static class Rasterizer
{
	private const int KindStubs = 1;
	private const int KindNoDropout = 2;
	private const int KindSmart = 4;

	private const int NoPoint = 0x7FFFFFFF;
	private const int MaxElements = 0x3FFF;
	private const int MaxSplineSpan = 0xC81;

	private static readonly int[] SplineLoss =
	[
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3
	];

	private static readonly int[] BitLength = [0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4];

	private readonly record struct Crossing( short Position, ushort Code );

	public static GlyphBitmap Render( List<int> x, List<int> y, List<bool> onCurve, List<int> contourEnds, int advance, int scanKind )
	{
		var scan = new Scan( x, y, onCurve, contourEnds );

		scan.RemoveDuplicates();
		scan.Measure( ref scanKind );

		var bitmap = new GlyphBitmap
		{
			Advance = advance,
			Left = scan.Left,
			Width = scan.Right - scan.Left,
			Top = scan.Top,
			Rows = scan.Top - scan.Bottom
		};

		bitmap.Pixels = new bool[bitmap.Width * bitmap.Rows];

		if ( scan.Bottom >= scan.Top )
			return bitmap;

		scan.Fill( bitmap.Pixels, scanKind );

		if ( scan.Overflow )
		{
			Array.Clear( bitmap.Pixels );
			scan.Fill( bitmap.Pixels, scanKind & ~KindSmart );
		}

		if ( scan.Failed )
			Array.Clear( bitmap.Pixels );

		return bitmap;
	}

	private static int Magnitude( int value )
	{
		var negated = -value;

		return negated < 0 ? value : negated;
	}

	private static int PowerOf2( int value )
	{
		var magnitude = (uint)Magnitude( value );

		if ( magnitude > 0xFFFF )
		{
			if ( magnitude > 0xFFFFFF )
				return magnitude < 0x10000000 ? BitLength[(int)magnitude >> 24] + 24 : BitLength[magnitude >> 28] + 28;

			return (int)magnitude < 0x100000 ? BitLength[(int)magnitude >> 16] + 16 : BitLength[(int)magnitude >> 20] + 20;
		}

		if ( (int)magnitude < 0x100 )
			return (int)magnitude > 0xF ? BitLength[(int)magnitude >> 4] + 4 : BitLength[magnitude];

		return (int)magnitude < 0x1000 ? BitLength[(int)magnitude >> 8] + 8 : BitLength[(int)magnitude >> 12] + 12;
	}

	private static int RoundedDivide( int denominator, long numerator )
	{
		long half = denominator / 2;

		if ( (denominator < 0) != (numerator < 0) )
			half = -half;

		if ( denominator == 0 )
			return numerator + half < 0 ? unchecked((int)0x80000000) : 0x7FFFFFFF;

		return (int)((numerator + half) / denominator);
	}

	private sealed class Scan
	{
		private readonly int[] _x;
		private readonly int[] _y;
		private readonly bool[] _onCurve;
		private readonly int[] _starts;
		private readonly int[] _ends;

		private List<Crossing>[] _horizontalOn;
		private List<Crossing>[] _horizontalOff;
		private List<Crossing>[] _verticalOn;
		private List<Crossing>[] _verticalOff;
		private List<Crossing>[] _horizontal;
		private List<Crossing>[] _vertical;

		private readonly List<int> _elementX = [];
		private readonly List<int> _elementY = [];

		private bool[] _pixels;
		private int _kind;
		private ushort _code;

		private int _previousX;
		private int _previousY;
		private int _currentX;
		private int _currentY;
		private int _nextX;
		private int _nextY;

		public int Left;
		public int Right;
		public int Top;
		public int Bottom;
		public bool Failed;
		public bool Overflow;

		public Scan( List<int> x, List<int> y, List<bool> onCurve, List<int> contourEnds )
		{
			_x = [.. x];
			_y = [.. y];
			_onCurve = [.. onCurve];
			_ends = [.. contourEnds];
			_starts = new int[_ends.Length];

			for ( var i = 1; i < _ends.Length; i++ )
				_starts[i] = _ends[i - 1] + 1;
		}

		public void RemoveDuplicates()
		{
			for ( var contour = 0; contour < _ends.Length; contour++ )
			{
				var first = _starts[contour];
				var end = _ends[contour];

				for ( var point = first; point < end; point++ )
				{
					if ( _x[point + 1] != _x[point] || _y[point + 1] != _y[point] )
						continue;

					for ( var move = point; move > first; move-- )
					{
						_x[move] = _x[move - 1];
						_y[move] = _y[move - 1];
						_onCurve[move] = _onCurve[move - 1];
					}

					first++;
					_starts[contour] = first;
					_onCurve[point + 1] = true;
				}

				if ( first == end || _x[end] != _x[first] || _y[end] != _y[first] )
					continue;

				_starts[contour]++;
				_onCurve[end] = true;
			}
		}

		public void Measure( ref int kind )
		{
			var minX = 0;
			var maxX = 0;
			var minY = 0;
			var maxY = 0;
			var empty = true;

			for ( var contour = 0; contour < _ends.Length; contour++ )
			{
				var start = _starts[contour];
				var end = _ends[contour];

				if ( start == end )
					continue;

				if ( empty )
				{
					minX = maxX = _x[start];
					minY = maxY = _y[start];
					empty = false;
				}

				for ( var point = start; point <= end; point++ )
				{
					minX = Math.Min( minX, _x[point] );
					maxX = Math.Max( maxX, _x[point] );
					minY = Math.Min( minY, _y[point] );
					maxY = Math.Max( maxY, _y[point] );
				}
			}

			Left = (minX + 0x1F) >> 6;
			Bottom = (minY + 0x1F) >> 6;
			Right = (maxX + 0x20) >> 6;
			Top = (maxY + 0x20) >> 6;

			if ( empty )
				return;

			if ( Left == Right )
			{
				Right++;
				kind &= ~KindStubs;
			}

			if ( Bottom == Top )
			{
				Top++;
				kind &= ~KindStubs;
			}
		}

		public void Fill( bool[] pixels, int kind )
		{
			_pixels = pixels;
			_kind = kind;

			Failed = false;
			Overflow = false;

			_elementX.Clear();
			_elementY.Clear();

			_horizontalOn = NewLists( Top - Bottom );
			_horizontalOff = NewLists( Top - Bottom );

			if ( (_kind & KindNoDropout) == 0 )
			{
				_verticalOn = NewLists( Right - Left );
				_verticalOff = NewLists( Right - Left );
			}

			for ( var contour = 0; contour < _ends.Length && !Stopped; contour++ )
				Contour( _starts[contour], _ends[contour] );

			if ( !Stopped )
				FillBitmap();
		}

		private static List<Crossing>[] NewLists( int count )
		{
			var lists = new List<Crossing>[count];

			for ( var i = 0; i < count; i++ )
				lists[i] = [];

			return lists;
		}

		private bool Stopped => Failed || Overflow;

		private bool Smart => (_kind & (KindNoDropout | KindSmart)) == KindSmart;

		private void Contour( int start, int end )
		{
			if ( start == end )
				return;

			int x;
			int y;
			int pointX;
			int pointY;
			int point;
			int last;
			bool on;

			if ( !_onCurve[end] )
			{
				x = _x[end - 1];
				y = _y[end - 1];
				pointX = _x[end];
				pointY = _y[end];

				if ( !_onCurve[end - 1] )
				{
					x = (x + 1 + pointX) >> 1;
					y = (y + 1 + pointY) >> 1;
				}

				point = start - 1;
				last = end;
				on = false;
			}
			else
			{
				x = _x[end];
				y = _y[end];
				point = start;
				last = end + 1;
				on = _onCurve[start];
				pointX = _x[start];
				pointY = _y[start];
			}

			_currentX = x;
			_currentY = y;
			_previousX = NoPoint;

			if ( Smart )
			{
				_elementX.Add( x );
				_elementY.Add( y );
			}

			while ( point < last )
			{
				if ( !on )
				{
					var next = point + 1;
					var nextX = _x[next];
					var nextY = _y[next];

					if ( !_onCurve[next] )
					{
						nextX = (nextX + 1 + pointX) >> 1;
						nextY = (nextY + 1 + pointY) >> 1;
					}
					else
					{
						next = point + 2;
					}

					Spline( x, y, pointX, pointY, nextX, nextY );

					x = nextX;
					y = nextY;
					point = next;
				}
				else
				{
					Advance( pointX, pointY );
					Line( x, y, pointX, pointY );

					x = pointX;
					y = pointY;
					point++;
				}

				if ( Stopped || point == last )
					break;

				pointX = _x[point];
				pointY = _y[point];
				on = _onCurve[point];
			}

			if ( Stopped )
				return;

			if ( (_currentY & 0x3F) == 0x20 )
				HorizontalEndpoint( _nextX, _nextY );

			if ( !Stopped && (_kind & KindNoDropout) == 0 && (_currentX & 0x3F) == 0x20 )
				VerticalEndpoint( _nextX, _nextY );
		}

		private void Advance( int x, int y )
		{
			if ( (_currentY & 0x3F) == 0x20 )
			{
				if ( _currentX == x && _currentY == y )
					return;

				if ( _previousX == NoPoint )
				{
					_nextX = x;
					_nextY = y;
				}
				else
				{
					HorizontalEndpoint( x, y );
				}
			}

			if ( (_kind & KindNoDropout) == 0 && (_currentX & 0x3F) == 0x20 )
			{
				if ( _currentX == x && _currentY == y )
					return;

				if ( _previousX == NoPoint )
				{
					_nextX = x;
					_nextY = y;
				}
				else
				{
					VerticalEndpoint( x, y );
				}
			}

			_previousX = _currentX;
			_previousY = _currentY;
			_currentX = x;
			_currentY = y;
		}

		private void HorizontalEndpoint( int nextX, int nextY )
		{
			if ( _currentY < nextY )
			{
				if ( _currentY <= _previousY )
				{
					if ( _currentY < _previousY )
					{
						HorizontalOnEndpoint();
						HorizontalOffEndpoint();
						return;
					}

					if ( _previousX <= _currentX )
						return;
				}

				HorizontalOnEndpoint();
				return;
			}

			if ( nextY < _currentY )
			{
				if ( _previousY < _currentY )
				{
					HorizontalOnEndpoint();
					HorizontalOffEndpoint();
					return;
				}

				if ( _previousY <= _currentY && _currentX <= _previousX )
					return;

				HorizontalOffEndpoint();
				return;
			}

			if ( _currentY <= _previousY )
			{
				if ( _currentY < _previousY || _previousX < _currentX )
				{
					if ( _currentX <= nextX )
						return;

					HorizontalOffEndpoint();
					return;
				}

				if ( _previousX <= _currentX )
					return;
			}

			if ( nextX <= _currentX )
				return;

			HorizontalOnEndpoint();
		}

		private void VerticalEndpoint( int nextX, int nextY )
		{
			if ( nextX < _currentX )
			{
				if ( _previousX <= _currentX )
				{
					if ( _previousX < _currentX )
					{
						VerticalOnEndpoint();
						VerticalOffEndpoint();
						return;
					}

					if ( _previousY <= _currentY )
						return;
				}

				VerticalOnEndpoint();
				return;
			}

			if ( _currentX < nextX )
			{
				if ( _currentX < _previousX )
				{
					VerticalOnEndpoint();
					VerticalOffEndpoint();
					return;
				}

				if ( _currentX <= _previousX && _currentY <= _previousY )
					return;

				VerticalOffEndpoint();
				return;
			}

			if ( _previousX <= _currentX )
			{
				if ( _previousX < _currentX || _previousY < _currentY )
				{
					if ( _currentY <= nextY )
						return;

					VerticalOffEndpoint();
					return;
				}

				if ( _previousY <= _currentY )
					return;
			}

			if ( nextY <= _currentY )
				return;

			VerticalOnEndpoint();
		}

		private bool SetCode( int type )
		{
			var count = _elementX.Count;

			if ( count - 1 > MaxElements )
			{
				Overflow = true;
				return false;
			}

			_code = (ushort)(((short)(count - 1) * 4) | type);

			return true;
		}

		private bool EndpointCode() => !Stopped && (!Smart || SetCode( 2 ));

		private void HorizontalOnEndpoint()
		{
			if ( !EndpointCode() )
				return;

			_horizontal = _horizontalOn;
			AddHorizontal( (_currentX + 0x1F) >> 6, _currentY >> 6 );
		}

		private void HorizontalOffEndpoint()
		{
			if ( !EndpointCode() )
				return;

			_horizontal = _horizontalOff;
			AddHorizontal( (_currentX + 0x20) >> 6, _currentY >> 6 );
		}

		private void VerticalOnEndpoint()
		{
			if ( !EndpointCode() )
				return;

			_vertical = _verticalOn;
			AddVertical( _currentX >> 6, (_currentY + 0x1F) >> 6 );
		}

		private void VerticalOffEndpoint()
		{
			if ( !EndpointCode() )
				return;

			_vertical = _verticalOff;
			AddVertical( _currentX >> 6, (_currentY + 0x20) >> 6 );
		}

		private void Insert( List<Crossing> list, int position )
		{
			var index = list.Count;

			while ( index > 0 && list[index - 1].Position > (short)position )
				index--;

			list.Insert( index, new Crossing( (short)position, _code ) );
		}

		private void AddHorizontal( int x, int y )
		{
			if ( Failed )
				return;

			if ( Left <= x && x <= Right && Bottom <= y && y < Top )
				Insert( _horizontal[y - Bottom], x );
			else
				Failed = true;
		}

		private void AddVertical( int x, int y )
		{
			if ( Failed )
				return;

			if ( Left <= x && x < Right && Bottom <= y && y <= Top )
				Insert( _vertical[x - Left], y );
			else
				Failed = true;
		}

		private void Line( int x1, int y1, int x2, int y2 )
		{
			if ( Stopped )
				return;

			int yDirection;
			int row;
			int yRemainder;
			int dy;
			int quadrant;
			int rowCount;

			var down = y2 < y1;

			if ( down )
			{
				var scanline = ((y1 - 0x21) & ~0x3F) + 0x20;

				yDirection = -1;
				yRemainder = y1 - scanline;
				dy = y1 - y2;
				row = scanline >> 6;
				rowCount = row - ((y2 + 0x20) >> 6);
				quadrant = 4;
			}
			else
			{
				var scanline = ((y1 + 0x20) & ~0x3F) + 0x20;

				yDirection = 1;
				yRemainder = scanline - y1;
				row = scanline >> 6;
				dy = y2 - y1;
				quadrant = 1;
				rowCount = ((y2 - 0x21) >> 6) - row;
			}

			var rowOffset = down ? 1 : 0;
			var tie = rowOffset;

			rowCount++;

			if ( y2 == y1 )
			{
				if ( (_kind & KindNoDropout) != 0 )
					return;

				row = x2 < x1 ? y1 - 1 : y1;
				rowCount = 0;
				row = (row + 0x20) >> 6;
			}

			int xDirection;
			int column;
			int xRemainder;
			int dx;
			int columnCount;

			var left = x2 < x1;

			if ( left )
			{
				var scanline = ((x1 - 0x21) & ~0x3F) + 0x20;

				tie = 1 - tie;
				xDirection = -1;
				xRemainder = x1 - scanline;
				column = scanline >> 6;
				dx = x1 - x2;
				quadrant = quadrant != 1 ? 3 : 2;
				columnCount = column - ((x2 + 0x20) >> 6);
			}
			else
			{
				var scanline = ((x1 + 0x20) & ~0x3F) + 0x20;

				xDirection = 1;
				dx = x2 - x1;
				xRemainder = scanline - x1;
				column = scanline >> 6;
				columnCount = ((x2 - 0x21) >> 6) - column;
			}

			var columnOffset = left ? 1 : 0;

			columnCount++;

			if ( x2 == x1 )
			{
				column = y1 < y2 ? x1 - 1 : x1;
				column = (column + 0x20) >> 6;
				columnCount = 0;
			}

			_horizontal = quadrant is 1 or 2 ? _horizontalOn : _horizontalOff;

			if ( (_kind & KindNoDropout) == 0 )
			{
				_vertical = quadrant is 2 or 3 ? _verticalOn : _verticalOff;

				if ( (_kind & KindSmart) != 0 )
				{
					if ( !SetCode( 0 ) )
						return;

					_elementX.Add( x2 );
					_elementY.Add( y2 );
				}

				if ( y1 == y2 )
				{
					for ( var i = 0; i < columnCount; i++ )
					{
						AddVertical( column, row );
						column += xDirection;
					}
				}
				else if ( x1 == x2 )
				{
					for ( var i = 0; i < rowCount; i++ )
					{
						AddHorizontal( column, row );
						row += yDirection;
					}
				}
				else
				{
					var control = tie + ((dx * yRemainder) - (dy * xRemainder));

					for ( var i = 0; i < rowCount + columnCount; i++ )
					{
						if ( control < 1 )
						{
							AddHorizontal( columnOffset + column, row );
							row += yDirection;
							control += dx << 6;
						}
						else
						{
							AddVertical( column, rowOffset + row );
							column += xDirection;
							control += dy * -0x40;
						}
					}
				}

				return;
			}

			if ( x1 == x2 )
			{
				for ( var i = 0; i < rowCount; i++ )
				{
					AddHorizontal( column, row );
					row += yDirection;
				}

				return;
			}

			column += columnOffset;

			var decision = tie + ((dx * yRemainder) - (dy * xRemainder));

			for ( var i = 0; i < columnCount + rowCount; i++ )
			{
				if ( decision < 1 )
				{
					AddHorizontal( column, row );
					row += yDirection;
					decision += dx * 0x40;
				}
				else
				{
					column += xDirection;
					decision += dy * -0x40;
				}
			}
		}

		private void Spline( int x1, int y1, int x2, int y2, int x3, int y3 )
		{
			while ( !Stopped )
			{
				var dx2 = x3 - x2;
				var dx1 = x2 - x1;
				var dy2 = y3 - y2;
				var dy1 = y2 - y1;

				if ( (dy1 >= 1 && dy2 <= -1) || (dy1 <= -1 && dy2 >= 1) )
				{
					var denominator = dy1 - dy2;
					var splitX = x1 + RoundedDivide( denominator, (long)dx1 * dy1 );

					x2 += RoundedDivide( denominator, (long)dx2 * dy1 );

					var middleX = RoundedDivide( denominator, (long)(x2 - splitX) * dy1 ) + splitX;

					y2 = y1 + RoundedDivide( denominator, (long)dy1 * dy1 );

					Spline( x1, y1, splitX, y2, middleX, y2 );

					x1 = middleX;
					y1 = y2;
					continue;
				}

				if ( (dx1 >= 1 && dx2 <= -1) || (dx1 <= -1 && dx2 >= 1) )
				{
					var denominator = dx1 - dx2;
					var splitY = y1 + RoundedDivide( denominator, (long)dy1 * dx1 );

					y2 += RoundedDivide( denominator, (long)dy2 * dx1 );

					var middleY = splitY + RoundedDivide( denominator, (long)(y2 - splitY) * dx1 );

					x2 = x1 + RoundedDivide( denominator, (long)dx1 * dx1 );

					Spline( x1, y1, x2, splitY, x2, middleY );

					x1 = x2;
					y1 = middleY;
					continue;
				}

				if ( Magnitude( x3 - x1 ) >= MaxSplineSpan || Magnitude( y3 - y1 ) >= MaxSplineSpan )
				{
					var firstY = (y1 + y2) >> 1;

					y2 = (y2 + y3) >> 1;

					var firstX = (x1 + x2) >> 1;
					var middleY = (firstY + y2) >> 1;

					x2 = (x2 + x3) >> 1;

					var middleX = (x2 + firstX) >> 1;

					Spline( x1, y1, firstX, firstY, middleX, middleY );

					x1 = middleX;
					y1 = middleY;
					continue;
				}

				Advance( x3, y3 );

				if ( dy2 * dx1 != dy1 * dx2 )
					Curve( x1, y1, x2, y2, x3, y3 );
				else
					Line( x1, y1, x3, y3 );

				return;
			}
		}

		private void Curve( int x1, int y1, int x2, int y2, int x3, int y3 )
		{
			if ( Stopped )
				return;

			int yDirection;
			int row;
			int yRemainder;
			int rowEnd;
			int controlDy;
			int dy;
			int quadrant;

			var down = y3 <= y1;

			if ( down )
			{
				var scanline = ((y1 - 0x21) & ~0x3F) + 0x20;

				quadrant = 4;
				yDirection = -1;
				yRemainder = y1 - scanline;
				rowEnd = ((y3 + 0x20) >> 6) - 1;
				controlDy = y1 - y2;
				dy = y1 - y3;
				row = scanline >> 6;
			}
			else
			{
				var scanline = ((y1 + 0x20) & ~0x3F) + 0x20;

				quadrant = 1;
				yDirection = 1;
				yRemainder = scanline - y1;
				rowEnd = ((y3 - 0x21) >> 6) + 1;
				controlDy = y2 - y1;
				dy = y3 - y1;
				row = scanline >> 6;
			}

			var rowOffset = down ? 1 : 0;

			int xDirection;
			int column;
			int xRemainder;
			int columnEnd;
			int controlDx;
			int dx;
			int tie;

			var left = x3 <= x1;

			if ( left )
			{
				var scanline = ((x1 - 0x21) & ~0x3F) + 0x20;

				tie = 1 - rowOffset;
				xDirection = -1;
				xRemainder = x1 - scanline;
				columnEnd = ((x3 + 0x20) >> 6) - 1;
				dx = x1 - x3;
				quadrant = quadrant != 1 ? 3 : 2;
				controlDx = x1 - x2;
				column = scanline >> 6;
			}
			else
			{
				var scanline = ((x1 + 0x20) & ~0x3F) + 0x20;

				tie = rowOffset;
				xDirection = 1;
				xRemainder = scanline - x1;
				columnEnd = ((x3 - 0x21) >> 6) + 1;
				dx = x3 - x1;
				controlDx = x2 - x1;
				column = scanline >> 6;
			}

			var columnOffset = left ? 1 : 0;

			_horizontal = quadrant is 1 or 2 ? _horizontalOn : _horizontalOff;

			var dropout = (_kind & KindNoDropout) == 0;

			if ( dropout )
			{
				_vertical = quadrant is 2 or 3 ? _verticalOn : _verticalOff;

				if ( (_kind & KindSmart) != 0 )
				{
					if ( !SetCode( 1 ) )
						return;

					_elementX.Add( x2 );
					_elementY.Add( y2 );
					_elementX.Add( x3 );
					_elementY.Add( y3 );
				}

				if ( column == columnEnd )
				{
					for ( ; row != rowEnd; row += yDirection )
						AddHorizontal( column + columnOffset, row );

					return;
				}

				if ( row == rowEnd )
				{
					for ( ; column != columnEnd; column += xDirection )
						AddVertical( column, row + rowOffset );

					return;
				}
			}
			else
			{
				if ( row == rowEnd )
					return;

				if ( column == columnEnd )
				{
					for ( ; row != rowEnd; row += yDirection )
						AddHorizontal( column + columnOffset, row );

					return;
				}
			}

			var cross = ((dy * controlDx) - (dx * controlDy)) * 2;
			var crossPower = PowerOf2( cross );
			var spanPower = PowerOf2( Math.Max( dx, dy ) );

			if ( spanPower > 12 || crossPower > 25 )
			{
				Failed = true;
				return;
			}

			var loss = SplineLoss[crossPower + spanPower];
			var shift = 6 - loss;

			if ( loss > 0 )
			{
				var half = 1 << (loss - 1);

				dx = (dx + half) >> loss;
				dy = (dy + half) >> loss;
				controlDx = (controlDx + half) >> loss;
				controlDy = (controlDy + half) >> loss;
				xRemainder = (xRemainder + half) >> loss;
				yRemainder = (yRemainder + half) >> loss;
				cross = ((dy * controlDx) - (dx * controlDy)) * 2;
			}

			if ( Magnitude( cross * dx ) > 0x22FFFFFF || Magnitude( cross * dy ) > 0x22FFFFFF )
			{
				Failed = true;
				return;
			}

			var a = dx - (controlDx * 2);
			var b = dy - (controlDy * 2);
			var crossY = cross * controlDy;
			var crossX = cross * controlDx;
			var one = 1 << shift;
			var bb = b * b;
			var aa = a * a;
			var ab = -(b * a);

			int stepX;
			int stepY;
			int stepXy;
			int value;
			int secondShift;

			if ( spanPower < 8 )
			{
				var twice = b * a * -2;

				stepX = (((one + (xRemainder * 2)) * bb) + (twice * yRemainder) + (crossY * 2)) << shift;
				stepY = ((twice * xRemainder) + ((one + (yRemainder * 2)) * aa) + (crossX * -2)) << shift;
				secondShift = shift * 2;
				stepXy = twice << secondShift;
				value = (((crossX * -2) + (aa * yRemainder)) * yRemainder) + (((bb * xRemainder) + (twice * yRemainder) + (crossY * 2)) * xRemainder);
			}
			else
			{
				secondShift = 5 - loss;
				stepXy = ab << shift;
				stepX = ((xRemainder + (one >> 1)) * bb) + (ab * yRemainder) + crossY;
				stepY = ((((one >> 1) + yRemainder) * aa) + (ab * xRemainder)) - crossX;
				value = (((((bb >> 1) * xRemainder) + (ab * yRemainder) + crossY) >> shift) * xRemainder) + (((((aa >> 1) * yRemainder) - crossX) >> shift) * yRemainder);
			}

			bb <<= secondShift;
			aa <<= secondShift;
			value += tie;

			var twiceBb = bb * 2;
			var twiceAa = aa * 2;

			if ( !dropout )
			{
				column += columnOffset;

				var end = columnEnd + columnOffset;

				while ( column != end )
				{
					if ( row == rowEnd )
						return;

					var horizontal = cross < 1 ? value < 0 || bb < stepX : !(value < 0 || aa < stepY);

					if ( horizontal )
					{
						AddHorizontal( column, row );
						value += stepY;
						row += yDirection;
						stepX += stepXy;
						stepY += twiceAa;
					}
					else
					{
						column += xDirection;
						value += stepX;
						stepX += twiceBb;
						stepY += stepXy;
					}
				}

				for ( ; row != rowEnd; row += yDirection )
					AddHorizontal( column, row );

				return;
			}

			while ( column != columnEnd )
			{
				if ( row == rowEnd )
				{
					for ( ; column != columnEnd; column += xDirection )
						AddVertical( column, rowOffset + row );

					break;
				}

				var horizontal = cross < 1 ? value < 0 || bb < stepX : !(value < 0 || aa < stepY);

				if ( horizontal )
				{
					AddHorizontal( columnOffset + column, row );
					value += stepY;
					row += yDirection;
					stepX += stepXy;
					stepY += twiceAa;
				}
				else
				{
					AddVertical( column, rowOffset + row );
					column += xDirection;
					value += stepX;
					stepX += twiceBb;
					stepY += stepXy;
				}
			}

			for ( ; row != rowEnd; row += yDirection )
				AddHorizontal( columnOffset + column, row );
		}

		private void SetPixel( int x, int y )
		{
			if ( x < Left || Right <= x || y < Bottom || Top <= y )
			{
				Failed = true;
				return;
			}

			_pixels[((Top - 1 - y) * (Right - Left)) + (x - Left)] = true;
		}

		private bool GetPixel( int x, int y )
		{
			if ( x < Left || Right <= x || y < Bottom || Top <= y )
				return false;

			return _pixels[((Top - 1 - y) * (Right - Left)) + (x - Left)];
		}

		private void FillBitmap()
		{
			var width = Right - Left;

			for ( var y = Top - 1; y >= Bottom; y-- )
			{
				var on = _horizontalOn[y - Bottom];
				var off = _horizontalOff[y - Bottom];
				var offset = (Top - 1 - y) * width;

				for ( var i = 0; i < on.Count && i < off.Count; i++ )
				{
					var start = (short)(on[i].Position - Left);
					var stop = (short)(off[i].Position - Left);

					if ( stop < start )
						(start, stop) = (stop, start);

					for ( var x = start; x < stop; x++ )
						_pixels[offset + x] = true;
				}
			}

			if ( (_kind & KindNoDropout) != 0 )
				return;

			for ( var y = Top - 1; y >= Bottom && !Failed; y-- )
			{
				var on = _horizontalOn[y - Bottom];
				var off = _horizontalOff[y - Bottom];

				for ( var i = 0; i < on.Count && i < off.Count && !Failed; i++ )
				{
					if ( on[i].Position == off[i].Position )
						HorizontalDropout( on[i], off[i], y );
				}
			}

			for ( var x = Left; x < Right && !Failed; x++ )
			{
				var on = _verticalOn[x - Left];
				var off = _verticalOff[x - Left];

				for ( int i = on.Count - 1, j = off.Count - 1; i >= 0 && j >= 0 && !Failed; i--, j-- )
				{
					if ( on[i].Position == off[j].Position )
						VerticalDropout( on[i], off[j], x );
				}
			}
		}

		private int HorizontalCrossings( int x, int y )
		{
			if ( y < Bottom || Top <= y )
				return 0;

			var on = _horizontalOn[y - Bottom];
			var off = _horizontalOff[y - Bottom];
			var count = 0;

			for ( var i = 0; i < on.Count; i++ )
			{
				if ( on[i].Position == (short)x )
					count++;

				if ( i < off.Count && off[i].Position == (short)x )
					count++;
			}

			return count;
		}

		private int VerticalCrossings( int x, int y )
		{
			if ( x < Left || Right <= x )
				return 0;

			var on = _verticalOn[x - Left];
			var off = _verticalOff[x - Left];
			var count = 0;

			for ( var i = 0; i < on.Count; i++ )
			{
				if ( on[i].Position == (short)y )
					count++;

				if ( i < off.Count && off[i].Position == (short)y )
					count++;
			}

			return count;
		}

		private void HorizontalDropout( Crossing on, Crossing off, int y )
		{
			int x = on.Position;
			var pixel = x - 1;

			if ( (_kind & KindStubs) != 0 )
			{
				if ( HorizontalCrossings( x, y + 1 ) + VerticalCrossings( pixel, y + 1 ) + VerticalCrossings( x, y + 1 ) < 2 )
					return;

				if ( HorizontalCrossings( x, y - 1 ) + VerticalCrossings( pixel, y ) + VerticalCrossings( x, y ) < 2 )
					return;
			}

			if ( (Left < x && GetPixel( pixel, y )) || (x < Right && GetPixel( x, y )) )
				return;

			if ( (_kind & KindSmart) != 0 )
			{
				if ( (on.Code & 3) == 3 || (off.Code & 3) == 3 )
				{
					Failed = true;
					return;
				}

				pixel = (HorizontalPosition( off.Code, y ) - 1 + HorizontalPosition( on.Code, y )) >> 7;
			}

			pixel = Math.Max( Left, pixel );

			if ( Right <= pixel )
				pixel = Right - 1;

			SetPixel( pixel, y );
		}

		private void VerticalDropout( Crossing on, Crossing off, int x )
		{
			int y = on.Position;

			if ( y < Bottom || Top < y )
				return;

			if ( (_kind & KindStubs) != 0 )
			{
				if ( VerticalCrossings( x - 1, y ) + HorizontalCrossings( x, y ) + HorizontalCrossings( x, y - 1 ) < 2 )
					return;

				if ( VerticalCrossings( x + 1, y ) + HorizontalCrossings( x + 1, y ) + HorizontalCrossings( x + 1, y - 1 ) < 2 )
					return;
			}

			if ( (y > Bottom && GetPixel( x, y - 1 )) || (Top > y && GetPixel( x, y )) )
				return;

			int pixel;

			if ( (_kind & KindSmart) == 0 )
			{
				pixel = y - 1;
			}
			else
			{
				if ( (on.Code & 3) == 3 || (off.Code & 3) == 3 )
				{
					Failed = true;
					return;
				}

				pixel = (VerticalPosition( on.Code, x ) - 1 + VerticalPosition( off.Code, x )) >> 7;
			}

			pixel = Math.Max( Bottom, pixel );

			if ( Top <= pixel )
				pixel = Top - 1;

			if ( Bottom <= pixel && pixel < Top )
				SetPixel( x, pixel );
		}

		private int HorizontalPosition( ushort code, int y )
		{
			var index = code >> 2;

			return (code & 3) switch
			{
				0 => RoundedDivide( _elementY[index + 1] - _elementY[index], (long)((y * 0x40) - _elementY[index] + 0x20) * (_elementX[index + 1] - _elementX[index]) ) + _elementX[index],
				1 => SplinePosition( (y * 0x40) + 0x20, _elementY[index], _elementY[index + 1], _elementY[index + 2], _elementX[index], _elementX[index + 1], _elementX[index + 2] ),
				_ => _elementX[index]
			};
		}

		private int VerticalPosition( ushort code, int x )
		{
			var index = code >> 2;

			return (code & 3) switch
			{
				0 => RoundedDivide( _elementX[index + 1] - _elementX[index], (long)((x * 0x40) - _elementX[index] + 0x20) * (_elementY[index + 1] - _elementY[index]) ) + _elementY[index],
				1 => SplinePosition( (x * 0x40) + 0x20, _elementX[index], _elementX[index + 1], _elementX[index + 2], _elementY[index], _elementY[index + 1], _elementY[index + 2] ),
				_ => _elementY[index]
			};
		}

		private static int SplinePosition( int target, int a0, int controlA, int a2, int b0, int controlB, int b2 )
		{
			int lowA;
			int highA;
			int lowB;
			int highB;

			if ( a0 < a2 )
			{
				lowA = a0;
				highA = a2;
				lowB = b0;
				highB = b2;
			}
			else
			{
				lowA = a2;
				highA = a0;
				lowB = b2;
				highB = b0;
			}

			while ( true )
			{
				var middleB = ((controlB * 2) + 1 + highB + lowB) >> 2;
				var middleA = (lowA + 1 + highA + (controlA * 2)) >> 2;

				if ( middleA > target )
				{
					controlB = (controlB + lowB) >> 1;
					controlA = (controlA + lowA) >> 1;
					highA = middleA;
					highB = middleB;
				}
				else if ( middleA < target )
				{
					controlB = (controlB + highB) >> 1;
					controlA = (controlA + highA) >> 1;
					lowB = middleB;
					lowA = middleA;
				}
				else
				{
					return middleB;
				}
			}
		}
	}
}
