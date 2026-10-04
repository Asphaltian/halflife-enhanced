namespace GoldSrc.Vgui1;

public interface InputSignal
{
	void CursorMoved( int x, int y, Panel panel );
	void CursorEntered( Panel panel );
	void CursorExited( Panel panel );
	void MousePressed( MouseCode code, Panel panel );
	void MouseDoublePressed( MouseCode code, Panel panel );
	void MouseReleased( MouseCode code, Panel panel );
	void MouseWheeled( int delta, Panel panel );
	void KeyPressed( KeyCode code, Panel panel );
	void KeyTyped( KeyCode code, Panel panel );
	void KeyReleased( KeyCode code, Panel panel );
	void KeyFocusTicked( Panel panel );
}

public interface RepaintSignal
{
	void PanelRepainted( Panel panel );
}

public interface FocusChangeSignal
{
	void FocusChanged( bool lost, Panel panel );
}

public interface TickSignal
{
	void Ticked();
}

public class Layout
{
	public virtual void PerformLayout( Panel panel )
	{
	}
}

public abstract class LayoutInfo
{
	public abstract LayoutInfo GetThis();
}

public class FocusNavGroup
{
	private readonly List<Panel> _panelDar = [];
	private int _currentIndex;

	public virtual void AddPanel( Panel panel )
	{
		if ( !_panelDar.Contains( panel ) )
			_panelDar.Add( panel );
	}

	public virtual void RequestFocusPrev()
	{
		if ( _panelDar.Count == 0 )
			return;

		_currentIndex--;

		if ( _currentIndex < 0 )
			_currentIndex = _panelDar.Count - 1;

		_panelDar[_currentIndex].RequestFocus();
	}

	public virtual void RequestFocusNext()
	{
		if ( _panelDar.Count == 0 )
			return;

		_currentIndex++;

		if ( _currentIndex >= _panelDar.Count )
			_currentIndex = 0;

		_panelDar[_currentIndex].RequestFocus();
	}

	public virtual void SetCurrentPanel( Panel panel )
	{
		var index = _panelDar.IndexOf( panel );

		if ( index >= 0 )
			_currentIndex = index;
	}
}
