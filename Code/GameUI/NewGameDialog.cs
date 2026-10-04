using System;

namespace GoldSrc.GameUI;

public sealed class NewGameDialog : Frame
{
	private readonly ComboBox _difficulty;

	public NewGameDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "NewGameDialog" )
	{
		SetHdProportional( true );
		SetBounds( 0, 0, 222, 160 );
		SetSizeable( false );
		SetTitle( "#GameUI_NewGame" );
		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );

		_difficulty = new ComboBox( vgui, this, "Difficulty", 3, false );
		_difficulty.AddItem( "#GameUI_Easy" );

		var medium = _difficulty.AddItem( "#GameUI_Medium" );

		_difficulty.AddItem( "#GameUI_Hard" );
		_difficulty.ActivateItem( medium );
		_difficulty.SetVisible( false );

		var cancel = new Button( vgui, this, "Cancel", "#GameUI_Cancel" );

		cancel.SetCommand( "Close" );

		var play = new Button( vgui, this, "Play", "#GameUI_Play" );

		play.SetEnabled( false );
		play.SetVisible( false );

		var playGame = new Button( vgui, this, "PlayGame", "#GameUI_PlayGame" );

		playGame.SetEnabled( false );
		playGame.SetVisible( false );

		var playGameAlt = new Button( vgui, this, "PlayGame_Alt", "#GameUI_PlayGame_Alt" );

		playGameAlt.SetVisible( false );

		LoadControlSettings( "Resource\\NewGameDialog.res" );

		if ( ModInfo.GetString( "game" ).Equals( "Half-Life", StringComparison.OrdinalIgnoreCase ) )
		{
			playGameAlt.SetVisible( true );
		}
		else
		{
			playGame.SetText( "#GameUI_Play" );
			playGameAlt.SetVisible( false );
			playGameAlt.MarkForDeletion();
		}

		if ( playGame.Visible )
		{
			playGame.SetEnabled( true );
			play.SetVisible( false );
			play.SetEnabled( false );
		}
		else
		{
			play.SetVisible( true );
			play.SetEnabled( true );
		}
	}

	public override void Close()
	{
		base.Close();

		MarkForDeletion();
	}

	public override void OnCommand( string command )
	{
		var skill = _difficulty.Visible ? _difficulty.GetActiveItem() + 1 : 0;

		if ( command.Equals( "Play", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( !_difficulty.Visible && skill < 1 )
				Cmd.AddText( $"disconnect\nmaxplayers 1\ndeathmatch 0\nmap {ModInfo.GetString( "trainmap" )}\n" );
			else
				Cmd.AddText( $"disconnect\nmaxplayers 1\ndeathmatch 0\nskill {skill}\nmap {ModInfo.GetString( "startmap" )}\n" );
		}
		else if ( command.Equals( "PlayGameAlt", StringComparison.OrdinalIgnoreCase ) )
		{
			Cmd.AddText( $"disconnect\nmaxplayers 1\ndeathmatch 0\nskill {skill}\nmap hldemo1.bsp" );
		}
		else if ( command.Equals( "PlayTraining", StringComparison.OrdinalIgnoreCase ) )
		{
			Cmd.AddText( $"disconnect\nmaxplayers 1\ndeathmatch 0\nmap {ModInfo.GetString( "trainmap" )}\n" );
		}
		else
		{
			base.OnCommand( command );
			return;
		}

		Close();
	}
}
