using System;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public class DefaultInputSignal : InputSignal
{
	public virtual void CursorMoved( int x, int y, Panel panel )
	{
	}

	public virtual void CursorEntered( Panel panel )
	{
	}

	public virtual void CursorExited( Panel panel )
	{
	}

	public virtual void MousePressed( MouseCode code, Panel panel )
	{
	}

	public virtual void MouseDoublePressed( MouseCode code, Panel panel )
	{
	}

	public virtual void MouseReleased( MouseCode code, Panel panel )
	{
	}

	public virtual void MouseWheeled( int delta, Panel panel )
	{
	}

	public virtual void KeyPressed( KeyCode code, Panel panel )
	{
	}

	public virtual void KeyTyped( KeyCode code, Panel panel )
	{
	}

	public virtual void KeyReleased( KeyCode code, Panel panel )
	{
	}

	public virtual void KeyFocusTicked( Panel panel )
	{
	}
}

public class CommandLabel : Label
{
	public CommandLabel( string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
	}

	public void PushUp() => Repaint();

	public void PushDown() => Repaint();
}

public class CommandButton : Button
{
	public const int MaxButtonSize = 32;

	private int _playerClass;
	private bool _flat;
	private CommandMenu _subMenu;
	private CommandMenu _parentMenu;
	private CommandLabel _subLabel;
	private string _mainText = "";
	private char _boundKey;

	public bool NoHighlight;

	public CommandButton( string text, int x, int y, int wide, int tall, bool noHighlight = false ) : base( "", x, y, wide, tall )
	{
		_playerClass = 0;
		NoHighlight = noHighlight;
		_flat = false;

		Init();
		SetText( text );
	}

	public CommandButton( int playerClass, string text, int x, int y, int wide, int tall, bool flat ) : base( "", x, y, wide, tall )
	{
		_playerClass = playerClass;
		NoHighlight = false;
		_flat = flat;

		Init();
		SetText( text );
	}

	public CommandButton( string text, int x, int y, int wide, int tall, bool noHighlight, bool flat ) : base( "", x, y, wide, tall )
	{
		_playerClass = 0;
		_flat = flat;
		NoHighlight = noHighlight;

		Init();
		SetText( text );
	}

	public void Init()
	{
		_subMenu = null;
		_subLabel = null;
		_parentMenu = null;

		SetFgColor( SchemeColor.Primary1 );
		SetContentAlignment( Alignment.West );

		if ( !NoHighlight )
			AddInputSignal( new HandlerCommandButtonHighlight( this ) );

		_boundKey = '\0';
	}

	private void RecalculateText()
	{
		string buffer;

		if ( _boundKey != '\0' )
			buffer = _boundKey == (char)255 ? _mainText : $"  {_boundKey}  {_mainText}";
		else
			buffer = $"     {_mainText}";

		base.SetText( VguiUtil.Truncate( buffer, MaxButtonSize ) );
	}

	public new void SetText( string text )
	{
		_mainText = VguiUtil.Truncate( text, MaxButtonSize );

		RecalculateText();
	}

	public void SetBoundKey( char boundKey )
	{
		_boundKey = boundKey;

		RecalculateText();
	}

	public char GetBoundKey() => _boundKey;

	public void AddSubMenu( CommandMenu newMenu )
	{
		_subMenu = newMenu;

		SetMouseClickEnabled( MouseCode.Left, false );
	}

	public void AddSubLabel( CommandLabel subLabel ) => _subLabel = subLabel;

	public virtual bool IsNotValid() => false;

	public void UpdateSubMenus( int adjustment ) => _subMenu?.RecalculatePositions( adjustment );

	public int GetPlayerClass() => _playerClass;

	public CommandMenu GetSubMenu() => _subMenu;

	public CommandMenu GetParentMenu() => _parentMenu;

	public void SetParentMenu( CommandMenu parentMenu ) => _parentMenu = parentMenu;

	public override void Paint()
	{
		if ( _subLabel is not null )
		{
			if ( IsSelected() )
				_subLabel.PushDown();
			else
				_subLabel.PushUp();
		}

		if ( IsArmed() )
			SetFgColor( SchemeColor.Secondary2 );
		else
			SetFgColor( SchemeColor.Primary1 );

		base.Paint();
	}

	protected void ButtonPaint() => base.Paint();

	public override void PaintBackground()
	{
		if ( _flat )
		{
			if ( IsArmed() )
			{
				DrawSetColor( SchemeColor.Secondary1 );
				DrawOutlinedRect( 0, 0, _wide, _tall );
			}
		}
		else
		{
			if ( IsArmed() )
			{
				DrawSetColor( SchemeColor.Primary2 );
				DrawFilledRect( 0, 0, _wide, _tall );
			}

			DrawSetColor( SchemeColor.Secondary1 );
			DrawOutlinedRect( 0, 0, _wide, _tall );
		}
	}

	public void CursorEntered()
	{
		var containingMenu = GetParentMenu();

		if ( containingMenu is not null )
		{
			containingMenu.ClearButtonsOfArmedState();

			var parent = containingMenu.GetParentMenu();

			if ( parent is not null )
			{
				var parentButton = parent.FindButtonWithSubmenu( containingMenu );

				parentButton.CursorEntered();
			}
		}

		SetArmed( true );
	}

	public void CursorExited()
	{
		if ( GetParentMenu() is null || GetSubMenu() is null )
			SetArmed( false );
	}
}

public class ColorButton : CommandButton
{
	private Color? _armedColor;
	private Color? _unArmedColor;
	private Color? _armedBorderColor;
	private Color? _unArmedBorderColor;

	public ColorButton( string text, int x, int y, int wide, int tall, bool noHighlight, bool flat ) : base( text, x, y, wide, tall, noHighlight, flat )
	{
	}

	public override void PaintBackground()
	{
		if ( IsArmed() )
		{
			if ( _armedBorderColor is { } color )
			{
				color.GetColor( out var r, out var g, out var b, out var a );

				DrawSetColor( r, g, b, a );
				DrawOutlinedRect( 0, 0, _wide, _tall );
			}
		}
		else
		{
			if ( _unArmedBorderColor is { } color )
			{
				color.GetColor( out var r, out var g, out var b, out var a );

				DrawSetColor( r, g, b, a );
				DrawOutlinedRect( 0, 0, _wide, _tall );
			}
		}
	}

	public override void Paint()
	{
		if ( IsArmed() )
		{
			if ( _armedColor is { } color )
			{
				color.GetColor( out var r, out var g, out var b, out var a );

				SetFgColor( r, g, b, a );
			}
			else
			{
				SetFgColor( SchemeColor.Secondary2 );
			}
		}
		else
		{
			if ( _unArmedColor is { } color )
			{
				color.GetColor( out var r, out var g, out var b, out var a );

				SetFgColor( r, g, b, a );
			}
			else
			{
				SetFgColor( SchemeColor.Primary1 );
			}
		}

		ButtonPaint();
	}

	public void SetArmedColor( int r, int g, int b, int a ) => _armedColor = new Color( r, g, b, a );

	public void SetUnArmedColor( int r, int g, int b, int a ) => _unArmedColor = new Color( r, g, b, a );

	public void SetArmedBorderColor( int r, int g, int b, int a ) => _armedBorderColor = new Color( r, g, b, a );

	public void SetUnArmedBorderColor( int r, int g, int b, int a ) => _unArmedBorderColor = new Color( r, g, b, a );
}

public class SpectButton : CommandButton
{
	public SpectButton( int playerClass, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall, false )
	{
		Init();
		SetText( text );
	}

	public override void PaintBackground()
	{
		if ( IsArmed() )
		{
			DrawSetColor( 143, 143, 54, 125 );
			DrawFilledRect( 5, 0, _wide - 5, _tall );
		}
	}

	public override void Paint()
	{
		if ( IsArmed() )
			SetFgColor( 194, 202, 54, 0 );
		else
			SetFgColor( 143, 143, 54, 15 );

		ButtonPaint();
	}
}

public class CommandMenu : Panel
{
	public const int MaxButtons = 100;

	private readonly CommandMenu _parentMenu;
	private int _xOffset;
	private int _yOffset;
	private readonly CommandButton[] _buttons = new CommandButton[MaxButtons];
	private int _buttonCount;
	private readonly int _direction;

	public float ButtonSizeY;
	public int SpectCmdMenu;

	public CommandMenu( CommandMenu parentMenu, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_parentMenu = parentMenu;
		_xOffset = x;
		_yOffset = y;
		_buttonCount = 0;
		_direction = 0;
	}

	public CommandMenu( CommandMenu parentMenu, int direction, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_parentMenu = parentMenu;
		_xOffset = x;
		_yOffset = y;
		_buttonCount = 0;
		_direction = direction;
	}

	public CommandMenu GetParentMenu() => _parentMenu;

	public int GetXOffset() => _xOffset;

	public int GetYOffset() => _yOffset;

	public int GetDirection() => _direction;

	public int GetNumButtons() => _buttonCount;

	public void AddButton( CommandButton button )
	{
		if ( _buttonCount >= MaxButtons )
			return;

		_buttons[_buttonCount] = button;
		_buttonCount++;

		button.SetParent( this );
		button.SetFont( SchemeFont.Primary3 );

		if ( _buttonCount < 10 )
			button.SetBoundKey( (char)(_buttonCount + '0') );
		else if ( _buttonCount == 10 )
			button.SetBoundKey( '0' );
	}

	public void RemoveAllButtons()
	{
		RemoveAllChildren();

		_buttonCount = 0;
	}

	public bool KeyInput( int keyNum )
	{
		for ( var i = 0; i < _buttonCount; i++ )
		{
			if ( _buttons[i].IsNotValid() )
				continue;

			if ( (sbyte)_buttons[i].GetBoundKey() != keyNum )
				continue;

			if ( _buttons[i].GetSubMenu() is not null )
			{
				VguiUtil.ViewPort.SetCurrentCommandMenu( _buttons[i].GetSubMenu() );

				return false;
			}

			_buttons[i].FireActionSignal();

			return true;
		}

		return false;
	}

	public void ClearButtonsOfArmedState()
	{
		for ( var i = 0; i < GetNumButtons(); i++ )
		{
			_buttons[i].SetArmed( false );
			_buttons[i].GetSubMenu()?.ClearButtonsOfArmedState();
		}
	}

	public CommandButton FindButtonWithSubmenu( CommandMenu subMenu )
	{
		for ( var i = 0; i < GetNumButtons(); i++ )
		{
			if ( _buttons[i].GetSubMenu() == subMenu )
				return _buttons[i];
		}

		return null;
	}

	public bool RecalculateVisibles( int yOffset, bool hideAll )
	{
		var currentY = 0;
		var visibleButtons = 0;

		for ( var i = 0; i < _buttonCount; i++ )
		{
			var playerClass = _buttons[i].GetPlayerClass();

			if ( (playerClass != 0 && playerClass != VguiUtil.Dll.PlayerClass) || _buttons[i].IsNotValid() || hideAll )
			{
				_buttons[i].SetVisible( false );
				_buttons[i].GetSubMenu()?.RecalculateVisibles( 0, true );
			}
			else
			{
				if ( _buttons[i].GetSubMenu() is not null && !_buttons[i].GetSubMenu().RecalculateVisibles( 0, false ) )
				{
					_buttons[i].SetVisible( false );
					continue;
				}

				_buttons[i].SetVisible( true );
				visibleButtons++;
			}
		}

		SetSize( _wide, (int)((visibleButtons * (ButtonSizeY - 1)) + 1) );

		if ( yOffset != 0 )
			_yOffset = yOffset;

		for ( var i = 0; i < _buttonCount; i++ )
		{
			if ( !_buttons[i].IsVisible() )
				continue;

			_buttons[i].GetSubMenu()?.RecalculateVisibles( currentY + _yOffset, false );

			if ( _direction != 0 )
				_buttons[i].SetPos( 0, (int)(((visibleButtons - 1) * (ButtonSizeY - 1)) - currentY) );
			else
				_buttons[i].SetPos( 0, currentY );

			currentY = (int)(currentY + (ButtonSizeY - 1));
		}

		return visibleButtons != 0;
	}

	public void RecalculatePositions( int yOffset )
	{
		int top;
		var adjust = 0;

		_yOffset += yOffset;

		if ( _direction != 0 )
			top = VguiUtil.ScreenHeight - (_yOffset + _tall);
		else
			top = _yOffset;

		if ( top < 0 )
			top = 0;

		var bottom = top + _tall;

		if ( bottom > VguiUtil.ScreenHeight )
		{
			while ( adjust < bottom - VguiUtil.ScreenHeight )
				adjust = (int)(adjust + (ButtonSizeY - 1));

			top -= adjust;

			if ( top < 0 )
			{
				adjust -= 0 - top;
				top = 0;
			}
		}

		SetPos( _x, top );

		for ( var i = 0; i < _buttonCount; i++ )
			_buttons[i].UpdateSubMenus( adjust );
	}

	public void MakeVisible( CommandMenu childMenu )
	{
		SetVisible( true );

		_parentMenu?.MakeVisible( this );
	}

	public override void PaintBackground()
	{
		if ( SpectCmdMenu != 0 )
			DrawSetColor( 0, 0, 0, 64 );
		else
			DrawSetColor( SchemeColor.Primary3 );

		DrawFilledRect( 0, 0, _wide, _tall );
	}
}

public class DropDownButton : ColorButton
{
	private readonly ImageLabel _openButton;

	public DropDownButton( string text, int x, int y, int wide, int tall, bool noHighlight, bool flat ) : base( text, x, y, wide, tall, noHighlight, flat )
	{
		_openButton = new ImageLabel( "arrowup", (int)VguiUtil.XRes( VguiUtil.CMenuSizeX - 2 ), (int)VguiUtil.YRes( VguiUtil.ButtonSizeY - 2 ) );
		_openButton.SetParent( this );

		GetSize( out var textWide, out _ );

		_openButton.SetPos( textWide - (_openButton.GetImageWide() + 6), -2 );
		_openButton.SetVisible( true );
	}

	public override void SetVisible( bool state )
	{
		_openButton.SetVisible( state );

		base.SetVisible( state );
	}
}

public class ImageButton : ColorButton
{
	private readonly ImageLabel _openButton;

	public ImageButton( string text, int x, int y, int wide, int tall, bool noHighlight, bool flat ) : base( " ", x, y, wide, tall, noHighlight, flat )
	{
		_openButton = new ImageLabel( text, 1, 1, wide - 2, tall - 2 );
		_openButton.SetParent( this );
		_openButton.SetVisible( true );
	}

	public override void SetVisible( bool state )
	{
		_openButton.SetVisible( state );

		base.SetVisible( state );
	}
}

public class MenuHandlerStringCommand : ActionSignal
{
	public const int MaxCommandSize = 256;

	protected readonly string _command;
	protected readonly bool _closeVguiMenu;

	public MenuHandlerStringCommand( string command )
	{
		_command = VguiUtil.Truncate( command, MaxCommandSize );
		_closeVguiMenu = false;
	}

	public MenuHandlerStringCommand( string command, int close )
	{
		_command = VguiUtil.Truncate( command, MaxCommandSize );
		_closeVguiMenu = true;
	}

	public virtual void ActionPerformed( Panel panel )
	{
		Client.ClientCmd( _command );

		if ( _closeVguiMenu )
			VguiUtil.ViewPort.HideTopMenu();
		else
			VguiUtil.ViewPort.HideCommandMenu();
	}
}

public class MenuHandlerStringCommandWatch : MenuHandlerStringCommand
{
	public MenuHandlerStringCommandWatch( string command ) : base( command )
	{
	}

	public MenuHandlerStringCommandWatch( string command, int close ) : base( command, close )
	{
	}

	public override void ActionPerformed( Panel panel )
	{
		base.ActionPerformed( panel );

		if ( _command == "jointeam 1" )
			VguiUtil.Dll.TeamNumber = 1;
		else if ( _command == "jointeam 2" )
			VguiUtil.Dll.TeamNumber = 2;
		else if ( _command == "jointeam 3" )
			VguiUtil.Dll.TeamNumber = 3;
		else if ( _command == "jointeam 4" )
			VguiUtil.Dll.TeamNumber = 4;
	}
}

public class MenuHandlerStringCommandClassSelect : MenuHandlerStringCommand
{
	public MenuHandlerStringCommandClassSelect( string command ) : base( command )
	{
	}

	public MenuHandlerStringCommandClassSelect( string command, int close ) : base( command, close )
	{
	}

	public override void ActionPerformed( Panel panel )
	{
		base.ActionPerformed( panel );

		if ( Hud.ClassAutoKill != 0f && VguiUtil.Dll.PlayerClass != 0 )
			Client.ClientCmd( "kill" );
	}
}

public class MenuHandlerPopupSubMenuInput( Button button, CommandMenu subMenu ) : DefaultInputSignal
{
	public override void CursorEntered( Panel panel )
	{
		VguiUtil.ViewPort.SetCurrentCommandMenu( subMenu );

		button?.SetArmed( true );
	}
}

public class MenuHandlerTextWindow( int state ) : ActionSignal
{
	public const int HideTextWindow = 0;
	public const int ShowMapBriefing = 1;
	public const int ShowClassDesc = 2;
	public const int ShowMotd = 3;
	public const int ShowSpecHelp = 4;

	public void ActionPerformed( Panel panel )
	{
		if ( state == HideTextWindow )
		{
			VguiUtil.ViewPort.HideTopMenu();
		}
		else
		{
			VguiUtil.ViewPort.HideCommandMenu();
			VguiUtil.ViewPort.ShowVGUIMenu( state );
		}
	}
}

public class MenuHandlerToggleCvar( string cvarName ) : ActionSignal
{
	private readonly string _cvar = Cvar.Exists( cvarName ) ? cvarName : null;

	public void ActionPerformed( Panel panel )
	{
		if ( Cvar.GetFloat( _cvar ) != 0f )
			Cvar.SetValue( _cvar, 0f );
		else
			Cvar.SetValue( _cvar, 1f );

		VguiUtil.ViewPort.HideCommandMenu();
		VguiUtil.ViewPort.UpdateSpectatorPanel();
	}
}

public class MenuHandlerSpectateFollow( string player ) : ActionSignal
{
	private readonly string _player = VguiUtil.Truncate( player, MenuHandlerStringCommand.MaxCommandSize );

	public void ActionPerformed( Panel panel )
	{
		VguiUtil.Hud.Spectator.FindPlayer( _player );
		VguiUtil.ViewPort.HideCommandMenu();
	}
}

public class HandlerMenuButtonOver( MenuPanel menuPanel, int button ) : DefaultInputSignal
{
	public override void CursorEntered( Panel panel )
	{
		if ( VguiUtil.ViewPort is not null && menuPanel is not null )
			menuPanel.SetActiveInfo( button );
	}
}

public class HandlerButtonHighlight( Button button ) : DefaultInputSignal
{
	public override void CursorEntered( Panel panel ) => button.SetArmed( true );

	public override void CursorExited( Panel panel ) => button.SetArmed( false );
}

public class HandlerCommandButtonHighlight( CommandButton button ) : HandlerButtonHighlight( button )
{
	public override void CursorEntered( Panel panel ) => button.CursorEntered();

	public override void CursorExited( Panel panel ) => button.CursorExited();
}

public class ClassButton : CommandButton
{
	protected int _classPlayerClass;

	public ClassButton( int playerClass, string text, int x, int y, int wide, int tall, bool noHighlight ) : base( text, x, y, wide, tall, noHighlight )
	{
		_classPlayerClass = playerClass;
	}

	public override bool IsNotValid()
	{
		var viewPort = VguiUtil.ViewPort;
		var team = VguiUtil.Dll.TeamNumber;

		if ( _classPlayerClass == -1 )
			return viewPort.GetValidClasses( team ) == -1;

		if ( (viewPort.GetValidClasses( 0 ) & TeamFortress.ValidClassInts[_classPlayerClass]) != 0 || (viewPort.GetValidClasses( team ) & TeamFortress.ValidClassInts[_classPlayerClass]) != 0 )
			return true;

		if ( Hud.ClassAutoKill != 0f )
		{
			if ( (viewPort.IsRandomPC() != 0 && _classPlayerClass == TeamFortress.PcRandom) || (viewPort.IsRandomPC() == 0 && _classPlayerClass == VguiUtil.Dll.PlayerClass) )
				return true;
		}

		return false;
	}
}

public class TeamButton : CommandButton
{
	private readonly int _teamNumber;

	public TeamButton( int team, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		_teamNumber = team;
	}

	public override bool IsNotValid()
	{
		var teams = VguiUtil.ViewPort.GetNumberOfTeams();

		if ( teams == 1 )
			return true;

		if ( _teamNumber == 5 )
			return false;

		if ( teams >= _teamNumber && _teamNumber != VguiUtil.Dll.TeamNumber )
			return false;

		return true;
	}
}

public class FeignButton : CommandButton
{
	private readonly int _feignState;

	public FeignButton( int state, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		_feignState = state;
	}

	public override bool IsNotValid()
	{
		if ( VguiUtil.Dll.PlayerClass != TeamFortress.PcSpy )
			return true;

		return _feignState != VguiUtil.ViewPort.GetIsFeigning();
	}
}

public class SpectateButton : CommandButton
{
	public SpectateButton( string text, int x, int y, int wide, int tall, bool noHighlight ) : base( text, x, y, wide, tall, noHighlight )
	{
	}

	public override bool IsNotValid() => VguiUtil.ViewPort.GetAllowSpectators() == 0;
}

public class DisguiseButton : CommandButton
{
	public const int DisguiseTeam1 = 1 << 0;
	public const int DisguiseTeam2 = 1 << 1;
	public const int DisguiseTeam3 = 1 << 2;
	public const int DisguiseTeam4 = 1 << 3;

	private readonly int _validTeamsBits;

	public DisguiseButton( int validTeamNumsBits, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall, false )
	{
		_validTeamsBits = validTeamNumsBits;
	}

	public override bool IsNotValid()
	{
		if ( VguiUtil.Dll.PlayerClass != TeamFortress.PcSpy )
			return true;

		if ( _validTeamsBits == 0 )
			return false;

		var bit = 1 << (VguiUtil.ViewPort.GetNumberOfTeams() - 1);

		return (_validTeamsBits & bit) == 0;
	}
}

public class DetpackButton : CommandButton
{
	private readonly int _detpackState;

	public DetpackButton( int state, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		_detpackState = state;
	}

	public override bool IsNotValid()
	{
		if ( VguiUtil.Dll.PlayerClass != TeamFortress.PcDemoman )
			return true;

		return _detpackState != VguiUtil.ViewPort.GetIsSettingDetpack();
	}
}

public class BuildButton : CommandButton
{
	public const int BuildStateHasBuilding = 1 << 0;
	public const int BuildStateBuilding = 1 << 1;
	public const int BuildStateBase = 1 << 2;
	public const int BuildStateCanBuild = 1 << 3;

	public const int Dispenser = 0;
	public const int SentryGun = 1;
	public const int EntryTeleporter = 2;
	public const int ExitTeleporter = 3;

	private readonly int _buildState;
	private readonly int _buildData;

	public BuildButton( int state, int data, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		_buildState = state;
		_buildData = data;
	}

	public override bool IsNotValid()
	{
		if ( VguiUtil.Dll.PlayerClass != TeamFortress.PcEngineer )
			return true;

		var state = VguiUtil.ViewPort.GetBuildState();

		if ( (_buildState & BuildStateBuilding) != 0 )
		{
			if ( (state & TeamFortress.BsBuilding) == 0 )
				return true;
		}
		else
		{
			if ( (state & TeamFortress.BsBuilding) != 0 )
				return true;
		}

		if ( (_buildState & BuildStateBase) != 0 )
		{
			const int any = TeamFortress.BsHasSentryGun | TeamFortress.BsHasDispenser | TeamFortress.BsCanBuildSentryGun | TeamFortress.BsCanBuildDispenser | TeamFortress.BsHasEntryTeleporter | TeamFortress.BsHasExitTeleporter | TeamFortress.BsCanBuildEntryTeleporter | TeamFortress.BsCanBuildExitTeleporter;

			return (state & any) == 0;
		}

		if ( (_buildState & BuildStateHasBuilding) != 0 )
		{
			if ( _buildData == Dispenser && (state & TeamFortress.BsHasDispenser) == 0 )
				return true;

			if ( _buildData == SentryGun && (state & TeamFortress.BsHasSentryGun) == 0 )
				return true;

			if ( _buildData == EntryTeleporter && (state & TeamFortress.BsHasEntryTeleporter) == 0 )
				return true;

			if ( _buildData == ExitTeleporter && (state & TeamFortress.BsHasExitTeleporter) == 0 )
				return true;
		}

		if ( (_buildState & BuildStateCanBuild) != 0 )
		{
			if ( _buildData == Dispenser && (state & TeamFortress.BsCanBuildDispenser) != 0 )
				return false;

			if ( _buildData == SentryGun && (state & TeamFortress.BsCanBuildSentryGun) != 0 )
				return false;

			if ( _buildData == EntryTeleporter && (state & TeamFortress.BsCanBuildEntryTeleporter) != 0 )
				return false;

			if ( _buildData == ExitTeleporter && (state & TeamFortress.BsCanBuildExitTeleporter) != 0 )
				return false;

			return true;
		}

		return false;
	}
}

public class MapButton : CommandButton
{
	private readonly string _mapName;

	public MapButton( string mapName, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		_mapName = $"maps/{mapName}.bsp";
	}

	public override bool IsNotValid()
	{
		var level = VguiUtil.Hud.Engine.LevelName;

		if ( level is null )
			return true;

		return _mapName != level;
	}
}

public class TeamOnlyCommandButton : CommandButton
{
	private readonly int _teamNum;

	public TeamOnlyCommandButton( int teamNum, string text, int x, int y, int wide, int tall, bool flat ) : base( text, x, y, wide, tall, false, flat )
	{
		_teamNum = teamNum;
	}

	public override bool IsNotValid()
	{
		if ( VguiUtil.Dll.TeamNumber != _teamNum )
			return true;

		return base.IsNotValid();
	}
}

public class ToggleCommandButton : CommandButton, InputSignal
{
	private readonly string _cvar;
	private readonly ImageLabel _labelOn;
	private readonly ImageLabel _labelOff;

	public ToggleCommandButton( string cvarName, string text, int x, int y, int wide, int tall, bool flat ) : base( text, x, y, wide, tall, false, flat )
	{
		_cvar = Cvar.Exists( cvarName ) ? cvarName : null;

		_labelOn = new ImageLabel( "checked", 0, 0 );
		_labelOn.SetParent( this );
		_labelOn.AddInputSignal( this );

		_labelOff = new ImageLabel( "unchecked", 0, 0 );
		_labelOff.SetParent( this );
		_labelOff.SetEnabled( true );
		_labelOff.AddInputSignal( this );

		GetTextSize( out var textWide, out _ );

		_labelOn.SetPos( textWide, (tall - _labelOn.GetTall()) / 2 );
		_labelOff.SetPos( textWide, (tall - _labelOff.GetTall()) / 2 );

		SetFgColor( SchemeColor.Primary1 );
	}

	public void CursorEntered( Panel panel ) => CursorEntered();

	public void CursorExited( Panel panel ) => CursorExited();

	public void MousePressed( MouseCode code, Panel panel ) => DoClick();

	public void CursorMoved( int x, int y, Panel panel )
	{
	}

	public void MouseDoublePressed( MouseCode code, Panel panel )
	{
	}

	public void MouseReleased( MouseCode code, Panel panel )
	{
	}

	public void MouseWheeled( int delta, Panel panel )
	{
	}

	public void KeyPressed( KeyCode code, Panel panel )
	{
	}

	public void KeyTyped( KeyCode code, Panel panel )
	{
	}

	public void KeyReleased( KeyCode code, Panel panel )
	{
	}

	public void KeyFocusTicked( Panel panel )
	{
	}

	public override void Paint()
	{
		if ( _cvar is null )
		{
			_labelOff.SetVisible( false );
			_labelOn.SetVisible( false );
		}
		else if ( Cvar.GetFloat( _cvar ) != 0f )
		{
			_labelOff.SetVisible( false );
			_labelOn.SetVisible( true );
		}
		else
		{
			_labelOff.SetVisible( true );
			_labelOn.SetVisible( false );
		}

		base.Paint();
	}
}

public class SpectToggleButton : CommandButton, InputSignal
{
	private readonly string _cvar;
	private readonly ImageLabel _labelOn;

	public SpectToggleButton( string cvarName, string text, int x, int y, int wide, int tall, bool flat ) : base( text, x, y, wide, tall, false, flat )
	{
		_cvar = Cvar.Exists( cvarName ) ? cvarName : null;

		_labelOn = new ImageLabel( "checked", 0, 0 );
		_labelOn.SetParent( this );
		_labelOn.AddInputSignal( this );

		GetTextSize( out var textWide, out _ );

		_labelOn.SetPos( textWide, (tall - _labelOn.GetTall()) / 2 );
	}

	public void CursorEntered( Panel panel ) => CursorEntered();

	public void CursorExited( Panel panel ) => CursorExited();

	public void MousePressed( MouseCode code, Panel panel ) => DoClick();

	public void CursorMoved( int x, int y, Panel panel )
	{
	}

	public void MouseDoublePressed( MouseCode code, Panel panel )
	{
	}

	public void MouseReleased( MouseCode code, Panel panel )
	{
	}

	public void MouseWheeled( int delta, Panel panel )
	{
	}

	public void KeyPressed( KeyCode code, Panel panel )
	{
	}

	public void KeyTyped( KeyCode code, Panel panel )
	{
	}

	public void KeyReleased( KeyCode code, Panel panel )
	{
	}

	public void KeyFocusTicked( Panel panel )
	{
	}

	public override void PaintBackground()
	{
		if ( IsArmed() )
		{
			DrawSetColor( 143, 143, 54, 125 );
			DrawFilledRect( 5, 0, _wide - 5, _tall );
		}
	}

	public override void Paint()
	{
		if ( IsArmed() )
			SetFgColor( 194, 202, 54, 0 );
		else
			SetFgColor( 143, 143, 54, 15 );

		if ( _cvar is null )
			_labelOn.SetVisible( false );
		else if ( Cvar.GetFloat( _cvar ) != 0f )
			_labelOn.SetVisible( true );
		else
			_labelOn.SetVisible( false );

		ButtonPaint();
	}
}

public class TransparentPanel : Panel
{
	private readonly int _transparency;

	public TransparentPanel( int trans, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_transparency = trans;
	}

	public override void PaintBackground()
	{
		if ( _transparency != 0 )
		{
			DrawSetColor( 0, 0, 0, _transparency );
			DrawFilledRect( 0, 0, _wide, _tall );
		}
	}
}

public class MenuPanel : TransparentPanel
{
	private MenuPanel _nextMenu;
	private int _menuId;
	private readonly int _removeMe;
	private bool _isActive;
	private float _openTime;

	public MenuPanel( int removeMe, int x, int y, int wide, int tall ) : base( 100, x, y, wide, tall )
	{
		MenuPanelReset();

		_removeMe = removeMe;
	}

	public MenuPanel( int trans, int removeMe, int x, int y, int wide, int tall ) : base( trans, x, y, wide, tall )
	{
		MenuPanelReset();

		_removeMe = removeMe;
	}

	private void MenuPanelReset()
	{
		_nextMenu = null;
		_isActive = false;
		_openTime = 0f;
	}

	public virtual void Reset() => MenuPanelReset();

	public void SetNextMenu( MenuPanel nextPanel )
	{
		if ( _nextMenu is not null )
			_nextMenu.SetNextMenu( nextPanel );
		else
			_nextMenu = nextPanel;
	}

	public void SetMenuID( int id ) => _menuId = id;

	public void SetActive( bool state ) => _isActive = state;

	public virtual void Open()
	{
		SetVisible( true );

		_openTime = VguiUtil.Hud.Time;
	}

	public virtual void Close()
	{
		SetVisible( false );

		_isActive = false;

		if ( _removeMe != 0 )
			VguiUtil.ViewPort.RemoveChild( this );
	}

	public int ShouldBeRemoved() => _removeMe;

	public MenuPanel GetNextMenu() => _nextMenu;

	public int GetMenuID() => _menuId;

	public bool IsActive() => _isActive;

	public float GetOpenTime() => _openTime;

	public virtual bool SlotInput( int slot ) => false;

	public virtual void SetActiveInfo( int input )
	{
	}
}

public class TFScrollButton : CommandButton
{
	private readonly BitmapTGA _tga;

	public TFScrollButton( int arrow, string text, int x, int y, int wide, int tall ) : base( text, x, y, wide, tall )
	{
		SetFgColor( SchemeColor.Primary1 );

		_tga = VguiUtil.LoadTgaForRes( VguiUtil.ArrowFilenames[arrow] );
		SetImage( _tga );

		AddInputSignal( new HandlerCommandButtonHighlight( this ) );
	}

	public override void Paint()
	{
		if ( _tga is null )
			return;

		if ( IsArmed() )
			_tga.SetColor( new Color( 255, 255, 255, 0 ) );
		else
			_tga.SetColor( new Color( 255, 255, 255, 128 ) );

		_tga.DoPaint( this );
	}

	public override void PaintBackground()
	{
	}
}

public class TFScrollPanel : ScrollPanel
{
	public TFScrollPanel( int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		var scrollBar = GetVerticalScrollBar();

		scrollBar.SetButton( new TFScrollButton( VguiUtil.ArrowUp, "", 0, 0, 16, 16 ), 0 );
		scrollBar.SetButton( new TFScrollButton( VguiUtil.ArrowDown, "", 0, 0, 16, 16 ), 1 );
		scrollBar.SetSlider( new TFSlider( 0, wide - 1, wide, (tall - (wide * 2)) + 2, true ) );
		scrollBar.SetPaintBorderEnabled( false );
		scrollBar.SetPaintBackgroundEnabled( false );
		scrollBar.SetPaintEnabled( false );

		scrollBar = GetHorizontalScrollBar();

		scrollBar.SetButton( new TFScrollButton( VguiUtil.ArrowLeft, "", 0, 0, 16, 16 ), 0 );
		scrollBar.SetButton( new TFScrollButton( VguiUtil.ArrowRight, "", 0, 0, 16, 16 ), 1 );
		scrollBar.SetSlider( new TFSlider( tall, 0, wide - (tall * 2), tall, false ) );
		scrollBar.SetPaintBorderEnabled( false );
		scrollBar.SetPaintBackgroundEnabled( false );
		scrollBar.SetPaintEnabled( false );
	}
}

public static class TeamFortress
{
	public const int PcUndefined = 0;
	public const int PcScout = 1;
	public const int PcSniper = 2;
	public const int PcSoldier = 3;
	public const int PcDemoman = 4;
	public const int PcMedic = 5;
	public const int PcHvyWeap = 6;
	public const int PcPyro = 7;
	public const int PcSpy = 8;
	public const int PcEngineer = 9;
	public const int PcRandom = 10;
	public const int PcCivilian = 11;
	public const int PcLastClass = 12;

	public const int BsBuilding = 1 << 0;
	public const int BsHasDispenser = 1 << 1;
	public const int BsHasSentryGun = 1 << 2;
	public const int BsCanBuildDispenser = 1 << 3;
	public const int BsCanBuildSentryGun = 1 << 4;
	public const int BsHasEntryTeleporter = 1 << 5;
	public const int BsHasExitTeleporter = 1 << 6;
	public const int BsCanBuildEntryTeleporter = 1 << 7;
	public const int BsCanBuildExitTeleporter = 1 << 8;

	[SkipHotload]
	public static readonly int[] ValidClassInts = [0, 1, 2, 4, 8, 16, 32, 64, 256, 512, 128];

	[SkipHotload]
	public static readonly string[] Classes =
	[
		"",
		"SCOUT",
		"SNIPER",
		"SOLDIER",
		"DEMOMAN",
		"MEDIC",
		"HWGUY",
		"PYRO",
		"SPY",
		"ENGINEER",
		"CIVILIAN"
	];

	[SkipHotload]
	public static readonly string[] LocalisedClasses =
	[
		"#Civilian",
		"#Scout",
		"#Sniper",
		"#Soldier",
		"#Demoman",
		"#Medic",
		"#HWGuy",
		"#Pyro",
		"#Spy",
		"#Engineer",
		"#Random",
		"#Civilian"
	];

	[SkipHotload]
	public static readonly string[] ClassSelection =
	[
		"civilian",
		"scout",
		"sniper",
		"soldier",
		"demoman",
		"medic",
		"hwguy",
		"pyro",
		"spy",
		"engineer",
		"randompc",
		"civilian"
	];
}
