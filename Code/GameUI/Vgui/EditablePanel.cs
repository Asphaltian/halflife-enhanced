using System;

namespace GoldSrc.GameUI;

public sealed class FocusNavGroup( Panel mainPanel )
{
	private const int MaxTabPosition = 9999999;
	private const int MaxFreeTabPosition = 999999;
	private const int MaxStackDepth = 15;

	private Panel _defaultButton;
	private Panel _currentDefaultButton;
	private Panel _currentFocus;
	private bool _topLevelFocus;
	private int _stackDepth;

	private static Panel Alive( Panel panel ) => panel is { Deleted: false } ? panel : null;

	public Panel DefaultButton => Alive( _defaultButton );

	public Panel CurrentDefaultButton => Alive( _currentDefaultButton );

	public Panel CurrentFocus => Alive( _currentFocus );

	public void SetFocusTopLevel( bool state ) => _topLevelFocus = state;

	public Panel GetDefaultPanel()
	{
		foreach ( var child in mainPanel.Children )
		{
			if ( child.TabPosition == 1 )
				return child;
		}

		return null;
	}

	public void SetDefaultButton( Panel panel )
	{
		if ( (panel is null && DefaultButton is null) || panel == DefaultButton )
			return;

		_defaultButton = panel;

		SetCurrentDefaultButton( DefaultButton, true );
	}

	public void SetCurrentDefaultButton( Panel panel, bool sendCurrentDefaultButtonMessage )
	{
		if ( panel == CurrentDefaultButton )
			return;

		if ( sendCurrentDefaultButtonMessage && CurrentDefaultButton is { } previous )
			mainPanel.PostMessage( previous, new Message( "SetAsCurrentDefaultButton" ) { Int = 0 } );

		_currentDefaultButton = panel;

		if ( sendCurrentDefaultButtonMessage && CurrentDefaultButton is { } current )
			mainPanel.PostMessage( current, new Message( "SetAsCurrentDefaultButton" ) { Int = 1 } );
	}

	public Panel SetCurrentFocus( Panel focus, Panel defaultPanel )
	{
		_currentFocus = focus;

		if ( defaultPanel is null )
		{
			if ( focus.CanBeDefaultButton )
			{
				SetCurrentDefaultButton( focus, true );

				return focus;
			}

			defaultPanel = DefaultButton;
		}

		SetCurrentDefaultButton( defaultPanel, true );

		return defaultPanel;
	}

	public Panel FindPanelByHotkey( char key )
	{
		foreach ( var child in mainPanel.Children )
		{
			if ( child.HasHotkey( key ) is { Visible: true, Enabled: true } panel )
				return panel;
		}

		return null;
	}

	private bool ParentTakesFocus => !_topLevelFocus && mainPanel.Parent is { } parent && parent != mainPanel.Vgui.Surface.EmbeddedPanel;

	private void FocusChild( Panel child, int direction )
	{
		_currentFocus = child;

		child.RequestFocus( direction );

		if ( child.CanBeDefaultButton )
		{
			SetCurrentDefaultButton( child, true );
		}
		else if ( DefaultButton is null )
		{
			SetCurrentDefaultButton( null, true );

			if ( mainPanel.Parent is { } parent )
				mainPanel.PostMessage( parent, new Message( "FindDefaultButton" ) );
		}
		else
		{
			SetCurrentDefaultButton( DefaultButton, true );
		}
	}

	public bool RequestFocusPrev( Panel panel )
	{
		_currentFocus = null;

		var newPosition = panel?.TabPosition ?? MaxTabPosition;
		var firstPass = true;
		Panel best = null;
		var bestPosition = 0;

		while ( true )
		{
			newPosition--;

			if ( newPosition > 0 )
			{
				foreach ( var child in mainPanel.Children )
				{
					if ( !child.Visible || !child.Enabled || child.TabPosition == 0 )
						continue;

					if ( child.TabPosition == newPosition )
					{
						best = child;
						break;
					}

					if ( child.TabPosition < newPosition && bestPosition < child.TabPosition )
					{
						bestPosition = child.TabPosition;
						best = child;
					}
				}

				if ( best is not null )
				{
					FocusChild( best, -1 );
					return true;
				}

				if ( !firstPass )
					return false;
			}

			if ( ParentTakesFocus && mainPanel.Parent.RequestFocusPrev( mainPanel ) )
			{
				SetCurrentDefaultButton( null, true );
				return true;
			}

			newPosition = MaxTabPosition;
			firstPass = false;
		}
	}

	public bool RequestFocusNext( Panel panel )
	{
		_stackDepth++;
		_currentFocus = null;

		var newPosition = panel?.TabPosition ?? 0;
		var firstPass = true;
		var found = false;

		while ( true )
		{
			Panel best = null;
			var bestPosition = MaxFreeTabPosition;

			foreach ( var child in mainPanel.Children )
			{
				if ( !child.Visible || !child.Enabled || child.TabPosition == 0 )
					continue;

				if ( child.TabPosition == newPosition + 1 )
				{
					best = child;
					break;
				}

				if ( child.TabPosition > newPosition + 1 && child.TabPosition < bestPosition )
				{
					bestPosition = child.TabPosition;
					best = child;
				}
			}

			if ( best is not null )
			{
				FocusChild( best, 1 );
				found = true;
				break;
			}

			if ( !firstPass )
				break;

			if ( ParentTakesFocus && _stackDepth < MaxStackDepth && mainPanel.Parent.RequestFocusNext( mainPanel ) )
			{
				SetCurrentDefaultButton( null, true );
				found = true;
				break;
			}

			newPosition = 0;
			firstPass = false;
		}

		_stackDepth--;

		return found;
	}
}

public sealed class BuildGroup( EditablePanel parentPanel )
{
	private readonly List<Panel> _panels = [];
	private readonly List<Panel> _createdPanels = [];

	public void PanelAdded( Panel panel )
	{
		if ( !_panels.Contains( panel ) )
			_panels.Add( panel );
	}

	public void PanelRemoved( Panel panel ) => _panels.Remove( panel );

	public void LoadControlSettings( string resourceName )
	{
		if ( KeyValues.LoadFromFile( resourceName ) is not { } resource )
			return;

		DeleteAllControlsCreatedByControlSettingsFile();

		ApplySettings( resource );

		parentPanel.InvalidateLayout();
	}

	private void DeleteAllControlsCreatedByControlSettingsFile()
	{
		foreach ( var panel in _createdPanels )
		{
			_panels.Remove( panel );

			if ( !panel.Deleted )
				panel.Delete();
		}

		_createdPanels.Clear();

		parentPanel.InvalidateLayout();
	}

	public void ApplySettings( KeyValues resource )
	{
		foreach ( var controlKeys in resource.Keys )
		{
			if ( controlKeys.Value is not null )
				continue;

			if ( _panels.Find( x => string.Equals( x.Name, controlKeys.Name, StringComparison.OrdinalIgnoreCase ) ) is { } existing )
			{
				existing.ApplySettings( controlKeys );
				continue;
			}

			if ( parentPanel.CreateControlByName( controlKeys.GetString( "ControlName" ) ) is not { } panel )
				continue;

			panel.SetParent( parentPanel );
			panel.SetPos( 0, 0 );
			panel.Name = controlKeys.Name;
			panel.ApplySettings( controlKeys );
			panel.AddActionSignalTarget( parentPanel );

			_createdPanels.Add( panel );
		}
	}
}

public class EditablePanel : Panel
{
	private int _oldWide;
	private int _oldTall;

	public FocusNavGroup NavGroup { get; }

	protected BuildGroup OwnBuildGroup { get; }

	public EditablePanel( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		NavGroup = new FocusNavGroup( this );
		OwnBuildGroup = new BuildGroup( this );

		_oldWide = Wide;
		_oldTall = Tall;
	}

	public virtual void LoadControlSettings( string resourceName )
	{
		OwnBuildGroup.LoadControlSettings( resourceName );

		InvalidateLayout();
	}

	public virtual Panel CreateControlByName( string controlName )
	{
		if ( controlName.Equals( "Label", StringComparison.OrdinalIgnoreCase ) )
			return new Label( Vgui, null, null, "Label" );

		if ( controlName.Equals( "Button", StringComparison.OrdinalIgnoreCase ) )
			return new Button( Vgui, null, null, "Button" );

		if ( controlName.Equals( "ComboBox", StringComparison.OrdinalIgnoreCase ) )
			return new ComboBox( Vgui, null, null, 5, true );

		if ( controlName.Equals( "CheckButton", StringComparison.OrdinalIgnoreCase ) )
			return new CheckButton( Vgui, null, null, "CheckButton" );

		if ( controlName.Equals( "RadioButton", StringComparison.OrdinalIgnoreCase ) )
			return new RadioButton( Vgui, null, null, "RadioButton" );

		if ( controlName.Equals( "TextEntry", StringComparison.OrdinalIgnoreCase ) )
			return new TextEntry( Vgui, null, null );

		if ( controlName.Equals( "ToggleButton", StringComparison.OrdinalIgnoreCase ) )
			return new ToggleButton( Vgui, null, null, "ToggleButton" );

		return null;
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		OwnBuildGroup.ApplySettings( settings );

		_oldWide = Wide;
		_oldTall = Tall;
	}

	public override void OnChildAdded( Panel child )
	{
		child.BuildGroup = OwnBuildGroup;
		OwnBuildGroup.PanelAdded( child );

		child.AddActionSignalTarget( this );
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		base.OnSizeChanged( wide, tall );

		var dx = wide - _oldWide;
		var dy = tall - _oldTall;

		foreach ( var child in Children )
		{
			var x = child.X;
			var y = child.Y;
			var childWide = child.Wide;
			var childTall = child.Tall;

			if ( child.PinCorner is 1 or 3 )
				x += dx;

			if ( child.PinCorner is 2 or 3 )
				y += dy;

			if ( child.AutoResize is 1 or 3 )
				childWide += dx;

			if ( child.AutoResize is 2 or 3 )
				childTall += dy;

			if ( x + childWide > wide )
			{
				childWide = wide - x;

				if ( childWide < 0 )
					continue;
			}

			if ( y + childTall > tall )
			{
				childTall = tall - y;

				if ( childTall < 0 )
					continue;
			}

			child.SetBounds( x, y, childWide, childTall );
			child.InvalidateLayout();
		}

		_oldWide = wide;
		_oldTall = tall;
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "DefaultButtonSet":
				NavGroup.SetDefaultButton( message.Panel );
				break;

			case "CurrentDefaultButtonSet":
				OnCurrentDefaultButtonSet( message.Panel );
				break;

			case "FindDefaultButton":
				OnFindDefaultButton();
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	private void OnCurrentDefaultButtonSet( Panel button )
	{
		NavGroup.SetCurrentDefaultButton( button, false );

		if ( Parent is not null )
			PostMessage( Parent, new Message( "CurrentDefaultButtonSet" ) { Panel = button } );
	}

	private void OnFindDefaultButton()
	{
		if ( NavGroup.DefaultButton is { } button )
			NavGroup.SetCurrentDefaultButton( button, true );
		else if ( Parent is not null )
			PostMessage( Parent, new Message( "FindDefaultButton" ) );
	}

	public override Panel HasHotkey( char key )
	{
		if ( !Visible || !Enabled )
			return null;

		foreach ( var child in Children )
		{
			if ( child.HasHotkey( key ) is { Visible: true, Enabled: true } panel )
				return panel;
		}

		return null;
	}

	public override Panel GetCurrentKeyFocus()
	{
		var focus = NavGroup.CurrentFocus;

		if ( focus == this )
			return null;

		if ( focus is null || focus.IsPopup )
			return base.GetCurrentKeyFocus();

		return focus.GetCurrentKeyFocus() ?? focus;
	}

	public override void RequestFocus( int direction = 0 )
	{
		if ( direction == 1 )
			RequestFocusNext();
		else if ( direction == -1 )
			RequestFocusPrev();
		else
			base.RequestFocus();
	}

	public override bool RequestFocusPrev( Panel panel = null ) => NavGroup.RequestFocusPrev( panel );

	public override bool RequestFocusNext( Panel panel = null ) => NavGroup.RequestFocusNext( panel );

	public override void OnRequestFocus( Panel subFocus, Panel defaultPanel )
	{
		if ( !subFocus.IsPopup )
			defaultPanel = NavGroup.SetCurrentFocus( subFocus, defaultPanel );

		Parent?.OnRequestFocus( this, defaultPanel );
	}

	public override void OnSetFocus()
	{
		if ( NavGroup.CurrentFocus is { } focus && focus != this )
		{
			focus.RequestFocus();
			return;
		}

		if ( NavGroup.GetDefaultPanel() is not { } panel )
			return;

		panel.RequestFocus();
		panel.OnSetFocus();
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( code is KeyCode.Enter or KeyCode.PadEnter && NavGroup.CurrentDefaultButton is { Visible: true, Enabled: true } button )
		{
			PostMessage( button, new Message( "Hotkey" ) );
			return;
		}

		base.OnKeyCodeTyped( code );
	}
}
