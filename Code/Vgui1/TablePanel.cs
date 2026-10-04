namespace GoldSrc.Vgui1;

public abstract class TablePanel : Panel
{
	private sealed class Handler( TablePanel table ) : ChangeSignal, InputSignal, RepaintSignal
	{
		public void ValueChanged( Panel panel )
		{
			var header = panel as HeaderPanel;
			var columnCount = table.GetColumnCount();

			for ( var i = 0; i < header.GetSectionCount() && i < columnCount; i++ )
			{
				header.GetSectionExtents( i, out var x0, out var x1 );
				table.SetColumnExtents( i, x0, x1 );
			}
		}

		public void CursorMoved( int x, int y, Panel panel )
		{
		}

		public void CursorEntered( Panel panel )
		{
		}

		public void CursorExited( Panel panel )
		{
		}

		public void MousePressed( MouseCode code, Panel panel ) => table.PrivateMousePressed( code, panel );

		public void MouseDoublePressed( MouseCode code, Panel panel ) => table.PrivateMouseDoublePressed( code, panel );

		public void MouseReleased( MouseCode code, Panel panel )
		{
		}

		public void MouseWheeled( int delta, Panel panel )
		{
		}

		public void KeyPressed( KeyCode code, Panel panel )
		{
		}

		public void KeyTyped( KeyCode code, Panel panel ) => table.PrivateKeyTyped( code, panel );

		public void KeyReleased( KeyCode code, Panel panel )
		{
		}

		public void KeyFocusTicked( Panel panel )
		{
		}

		public void PanelRepainted( Panel panel ) => table.Repaint();
	}

	private const int ExtentBits = 12;
	private const int ExtentMask = 0xfff;
	private const int DefaultGridSize = 2;

	private readonly Dar<int> _columnDar = new();
	private bool _gridVisibleHorizontal;
	private bool _gridVisibleVertical;
	private int _gridWide;
	private int _gridTall;
	private int _selectedColumn;
	private int _selectedRow;
	private int _mouseOverColumn;
	private int _mouseOverRow;
	private int _editableColumn;
	private int _editableRow;
	private readonly Panel _fakeInputPanel;
	private bool _columnSelectionEnabled;
	private bool _rowSelectionEnabled;
	private bool _cellSelectionEnabled;
	private Panel _editableCellPanel;
	private int _virtualWide;
	private int _virtualTall;
	private bool _cellEditingEnabled;

	protected TablePanel( int x, int y, int wide, int tall, int columnCount ) : base( x, y, wide, tall )
	{
		_gridWide = DefaultGridSize;
		_gridTall = DefaultGridSize;
		_gridVisibleHorizontal = false;
		_gridVisibleVertical = false;

		_columnDar.EnsureCapacity( columnCount );
		_columnDar.SetCount( columnCount );

		base.SetFgColor( new Color( SchemeColor.Black ) );

		_selectedColumn = -1;
		_selectedRow = -1;
		_fakeInputPanel = new Panel();
		_columnSelectionEnabled = false;
		_rowSelectionEnabled = true;
		_cellSelectionEnabled = true;
		_mouseOverColumn = 0;
		_mouseOverRow = 0;
		_editableColumn = -1;
		_editableRow = -1;
		_editableCellPanel = null;
		_virtualWide = wide;
		_virtualTall = tall;
		_cellEditingEnabled = true;

		AddInputSignal( new Handler( this ) );
	}

	public virtual void SetCellEditingEnabled( bool state )
	{
		_cellEditingEnabled = state;
	}

	public virtual void SetColumnCount( int columnCount )
	{
		_columnDar.EnsureCapacity( columnCount );
		_columnDar.SetCount( columnCount );
	}

	public virtual void SetGridVisible( bool horizontal, bool vertical )
	{
		_gridVisibleHorizontal = horizontal;
		_gridVisibleVertical = horizontal;
	}

	public virtual void SetGridSize( int gridWide, int gridTall )
	{
		_gridWide = gridWide;
		_gridTall = gridTall;
	}

	public virtual int GetColumnCount() => _columnDar.GetCount();

	public virtual void SetColumnExtents( int column, int x0, int x1 )
	{
		_columnDar.SetElementAt( (x0 << ExtentBits) + x1, column );

		Repaint();
	}

	public virtual void SetSelectedCell( int column, int row )
	{
		if ( column != _selectedColumn || row != _selectedRow )
		{
			Repaint();
			StopCellEditing();
		}

		_selectedColumn = column;
		_selectedRow = row;
	}

	public virtual void GetSelectedCell( out int column, out int row )
	{
		column = _selectedColumn;
		row = _selectedRow;
	}

	public virtual void SetHeaderPanel( HeaderPanel header )
	{
		header.AddChangeSignal( new Handler( this ) );
		header.FireChangeSignal();

		Repaint();
	}

	public virtual void SetColumnSelectionEnabled( bool state )
	{
		_columnSelectionEnabled = state;

		Repaint();
	}

	public virtual void SetRowSelectionEnabled( bool state )
	{
		_rowSelectionEnabled = state;

		Repaint();
	}

	public virtual void SetCellSectionEnabled( bool state )
	{
		_cellSelectionEnabled = state;

		Repaint();
	}

	public virtual void SetEditableCell( int column, int row )
	{
		if ( column != _editableColumn || row != _editableRow )
		{
			StopCellEditing();

			_editableCellPanel = StartCellEditing( column, row );
			_editableCellPanel?.SetParent( this );
		}

		_editableColumn = column;
		_editableRow = row;
	}

	public virtual void StopCellEditing()
	{
		if ( _editableCellPanel is null )
			return;

		_editableCellPanel.SetParent( null );
		_editableCellPanel = null;
		_editableColumn = -1;
		_editableRow = -1;
	}

	public override void GetVirtualSize( out int wide, out int tall )
	{
		wide = _virtualWide;
		tall = _virtualTall;
	}

	public abstract int GetRowCount();

	public abstract int GetCellTall( int row );

	public abstract Panel GetCellRenderer( int column, int row, bool columnSelected, bool rowSelected, bool cellSelected );

	public abstract Panel StartCellEditing( int column, int row );

	public override void Paint()
	{
		var x0Gap = (_gridWide / 2) - 1;
		var x1Gap = _gridWide - x0Gap;

		GetPaintSize( out var wide, out var tall );
		GetFgColor( out Color fgColor );

		fgColor.GetColor( out var r, out var g, out var b, out var a );

		if ( _gridVisibleVertical )
		{
			for ( var i = 0; i < _columnDar.GetCount(); i++ )
			{
				var x1 = _columnDar[i] & ExtentMask;

				DrawSetColor( r, g, b, a );
				DrawFilledRect( x1 - x1Gap, 0, x1 + x0Gap, tall );
			}
		}

		if ( _gridVisibleHorizontal )
		{
			var y = 0;

			for ( var j = 0; j < GetRowCount(); j++ )
			{
				DrawSetColor( r, g, b, a );
				DrawFilledRect( 0, y, wide, _gridTall + y );

				y += GetCellTall( j ) + _gridTall;
			}
		}

		_virtualWide = 0;
		_virtualTall = 0;

		for ( var i = 0; i < _columnDar.GetCount(); i++ )
		{
			var x0 = ((_columnDar[i] >> ExtentBits) & ExtentMask) + x0Gap;
			var x1 = (_columnDar[i] & ExtentMask) - x1Gap;

			if ( _virtualWide < x1 )
				_virtualWide = x1;

			var y = _gridTall;

			for ( var j = 0; j < GetRowCount(); j++ )
			{
				var columnSelected = _columnSelectionEnabled && _selectedColumn == i;
				var rowSelected = _rowSelectionEnabled && _selectedRow == j;
				var cellSelected = _cellSelectionEnabled && _selectedColumn == i && _selectedRow == j;

				if ( _editableColumn == i && _editableRow == j )
				{
					if ( _editableCellPanel is not null )
					{
						_editableCellPanel.SetBounds( x0, y, x1 - x0, GetCellTall( j ) );
						_editableCellPanel.Repaint();
						_editableCellPanel.SolveTraverse();
					}
				}
				else if ( GetCellRenderer( i, j, columnSelected, rowSelected, cellSelected ) is { } panel )
				{
					panel.SetParent( this );
					panel.SetBounds( x0, y, x1 - x0, GetCellTall( j ) );
					panel.Repaint();
					panel.SolveTraverse();
					panel.PaintTraverse();
					panel.SetParent( null );
				}

				y += GetCellTall( j ) + _gridTall;

				if ( _virtualTall < y )
					_virtualTall = x1;
			}
		}
	}

	public override Panel IsWithinTraverse( int x, int y )
	{
		var panel = base.IsWithinTraverse( x, y );

		if ( panel != this )
			return panel;

		var x0Gap = (_gridWide / 2) - 1;
		var x1Gap = _gridWide - x0Gap;

		for ( var i = 0; i < _columnDar.GetCount(); i++ )
		{
			var x0 = ((_columnDar[i] >> ExtentBits) & ExtentMask) + x0Gap;
			var x1 = (_columnDar[i] & ExtentMask) - x1Gap;
			var cellY = _gridTall;

			for ( var j = 0; j < GetRowCount(); j++ )
			{
				_fakeInputPanel.SetParent( this );
				_fakeInputPanel.SetBounds( x0, cellY, x1 - x0, GetCellTall( j ) );
				_fakeInputPanel.SolveTraverse();

				var hit = _fakeInputPanel.IsWithinTraverse( x, y );

				_fakeInputPanel.SetParent( null );

				if ( hit == _fakeInputPanel )
				{
					_mouseOverColumn = i;
					_mouseOverRow = j;

					return this;
				}

				cellY += GetCellTall( j ) + _gridTall;
			}
		}

		return this;
	}

	private void PrivateMousePressed( MouseCode code, Panel panel )
	{
		if ( !_cellEditingEnabled )
			return;

		SetSelectedCell( _mouseOverColumn, _mouseOverRow );
		RequestFocus();
	}

	private void PrivateMouseDoublePressed( MouseCode code, Panel panel )
	{
		if ( !_cellEditingEnabled )
			return;

		SetSelectedCell( _mouseOverColumn, _mouseOverRow );
		GetSelectedCell( out var column, out var row );
		SetEditableCell( column, row );
	}

	private void PrivateKeyTyped( KeyCode code, Panel panel )
	{
		if ( !_cellEditingEnabled )
			return;

		GetSelectedCell( out var column, out var row );

		switch ( code )
		{
			case KeyCode.Enter:
				SetEditableCell( column, row );
				break;
			case KeyCode.Up:
				row--;

				if ( row < 0 )
					row = 0;

				SetSelectedCell( column, row );
				break;
			case KeyCode.Left:
				column--;

				if ( column < 0 )
					column = 0;

				SetSelectedCell( column, row );
				break;
			case KeyCode.Down:
				SetSelectedCell( column, row + 1 );
				break;
			case KeyCode.Right:
				SetSelectedCell( column + 1, row );
				break;
		}
	}
}
