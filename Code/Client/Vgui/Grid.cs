using GoldSrc.Vgui1;

namespace HalfLife;

public class Grid : Panel
{
	protected sealed class GridEntryData
	{
		public Panel Panel;
		public bool Underline;
		public readonly short[] UnderlineColor = new short[4];
		public int UnderlineOffset;
	}

	protected bool _dirty;
	protected int _xCols;
	protected int _yRows;
	protected int _xSpacing;
	protected int _ySpacing;
	protected int[] _widths;
	protected int[] _heights;
	protected int[] _colOffsets;
	protected int[] _rowOffsets;
	protected GridEntryData[] _gridEntries;

	public Grid()
	{
		Clear();
	}

	public bool SetDimensions( int xCols, int yRows )
	{
		Term();

		_gridEntries = new GridEntryData[xCols * yRows];

		for ( var i = 0; i < _gridEntries.Length; i++ )
			_gridEntries[i] = new GridEntryData();

		_widths = new int[xCols];
		_heights = new int[yRows];
		_colOffsets = new int[xCols];
		_rowOffsets = new int[yRows];

		_xCols = xCols;
		_yRows = yRows;

		return true;
	}

	public void Term()
	{
		Clear();
	}

	public Panel GetEntry( int x, int y ) => GridEntry( x, y ).Panel;

	public bool SetEntry( int x, int y, Panel panel )
	{
		var entry = GridEntry( x, y );

		if ( entry is null )
			return false;

		entry.Panel?.SetParent( null );

		entry.Panel = panel;

		panel?.SetParent( this );

		_dirty = true;

		return true;
	}

	public int GetXSpacing() => _xSpacing;

	public int GetYSpacing() => _ySpacing;

	public void SetSpacing( int xSpacing, int ySpacing )
	{
		if ( xSpacing != _xSpacing )
		{
			_xSpacing = xSpacing;
			CalcColOffsets( 0 );
			_dirty = true;
		}

		if ( ySpacing != _ySpacing )
		{
			_ySpacing = ySpacing;
			CalcRowOffsets( 0 );
			_dirty = true;
		}
	}

	public bool SetColumnWidth( int column, int width )
	{
		if ( column < 0 || column >= _xCols )
			return false;

		_widths[column] = width;
		CalcColOffsets( column + 1 );
		_dirty = true;

		return true;
	}

	public bool SetRowHeight( int row, int height )
	{
		if ( row < 0 || row >= _yRows )
			return false;

		_heights[row] = height;
		CalcRowOffsets( row + 1 );
		_dirty = true;

		return true;
	}

	public int GetColumnWidth( int column )
	{
		if ( column < 0 || column >= _xCols )
			return 0;

		return _widths[column];
	}

	public int GetRowHeight( int row )
	{
		if ( row < 0 || row >= _yRows )
			return 0;

		return _heights[row];
	}

	public int CalcFitColumnWidth( int column )
	{
		if ( column < 0 || column >= _xCols )
			return 0;

		var maxSize = 0;

		for ( var i = 0; i < _yRows; i++ )
		{
			var panel = GridEntry( column, i ).Panel;

			if ( panel is null )
				continue;

			panel.GetSize( out var w, out _ );

			if ( w > maxSize )
				maxSize = w;
		}

		return maxSize;
	}

	public int CalcFitRowHeight( int row )
	{
		if ( row < 0 || row >= _yRows )
			return 0;

		var maxSize = 0;

		for ( var i = 0; i < _xCols; i++ )
		{
			var panel = GridEntry( i, row ).Panel;

			if ( panel is null )
				continue;

			panel.GetSize( out _, out var h );

			if ( h > maxSize )
				maxSize = h;
		}

		return maxSize;
	}

	public int CalcDrawHeight()
	{
		if ( _yRows > 0 )
			return _rowOffsets[_yRows - 1] + _heights[_yRows - 1] + _ySpacing;

		return 0;
	}

	public void AutoSetRowHeights()
	{
		for ( var i = 0; i < _yRows; i++ )
			SetRowHeight( i, CalcFitRowHeight( i ) );
	}

	public bool GetEntryBox( int col, int row, out int x, out int y, out int w, out int h )
	{
		x = y = w = h = 0;

		if ( col < 0 || col >= _xCols || row < 0 || row >= _yRows )
			return false;

		x = _colOffsets[col];
		w = _widths[col];

		y = _rowOffsets[row];
		h = _heights[row];

		return true;
	}

	public bool CopyColumnWidths( Grid other )
	{
		if ( other is null || other._xCols != _xCols )
			return false;

		for ( var i = 0; i < _xCols; i++ )
			_widths[i] = other._widths[i];

		CalcColOffsets( 0 );
		_dirty = true;

		return true;
	}

	public void RepositionContents()
	{
		for ( var x = 0; x < _xCols; x++ )
		{
			for ( var y = 0; y < _yRows; y++ )
			{
				var panel = GridEntry( x, y ).Panel;

				panel?.SetBounds( _colOffsets[x], _rowOffsets[y], _widths[x], _heights[y] );
			}
		}

		_dirty = false;
	}

	public void SetRowUnderline( int row, bool enabled, int offset, int r, int g, int b, int a )
	{
		var cell = GridEntry( 0, row );

		cell.Underline = enabled;

		if ( enabled )
		{
			cell.UnderlineOffset = offset;
			cell.UnderlineColor[0] = (short)r;
			cell.UnderlineColor[1] = (short)g;
			cell.UnderlineColor[2] = (short)b;
			cell.UnderlineColor[3] = (short)a;
		}
	}

	public bool GetCellAtPoint( int worldX, int worldY, out int row, out int col )
	{
		row = -1;
		col = -1;

		for ( var x = 0; x < _xCols; x++ )
		{
			for ( var y = 0; y < _yRows; y++ )
			{
				var panel = GridEntry( x, y ).Panel;

				if ( panel is null )
					continue;

				if ( panel.IsWithin( worldX, worldY ) )
				{
					col = x;
					row = y;

					return true;
				}
			}
		}

		return false;
	}

	public override void Paint()
	{
		if ( _dirty )
			RepositionContents();

		base.Paint();

		var y = 0;

		for ( var row = 0; row < _yRows; row++ )
		{
			var cell = GridEntry( 0, row );

			y += cell.Panel.GetTall() + _ySpacing;

			if ( cell.Underline )
			{
				DrawSetColor( cell.UnderlineColor[0], cell.UnderlineColor[1], cell.UnderlineColor[2], cell.UnderlineColor[3] );
				DrawFilledRect( 0, y - (cell.UnderlineOffset + 1), GetWide(), y - cell.UnderlineOffset );
			}
		}
	}

	public override void PaintBackground()
	{
		base.PaintBackground();
	}

	protected void Clear()
	{
		_xCols = _yRows = 0;
		_widths = null;
		_heights = null;
		_colOffsets = null;
		_rowOffsets = null;
		_gridEntries = null;
		_xSpacing = _ySpacing = 0;
		_dirty = false;
	}

	protected GridEntryData GridEntry( int x, int y )
	{
		if ( x < 0 || x >= _xCols || y < 0 || y >= _yRows )
			return null;

		return _gridEntries[(y * _xCols) + x];
	}

	protected void CalcColOffsets( int start )
	{
		var cur = _xSpacing;

		if ( start != 0 )
			cur += _colOffsets[start - 1] + _widths[start - 1];

		for ( var i = start; i < _xCols; i++ )
		{
			_colOffsets[i] = cur;
			cur += _widths[i] + _xSpacing;
		}
	}

	protected void CalcRowOffsets( int start )
	{
		var cur = _ySpacing;

		if ( start != 0 )
			cur += _rowOffsets[start - 1];

		for ( var i = start; i < _yRows; i++ )
		{
			_rowOffsets[i] = cur;
			cur += _heights[i] + _ySpacing;
		}
	}
}
