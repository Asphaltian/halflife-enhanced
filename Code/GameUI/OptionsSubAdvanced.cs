using System;
using System.Security.Cryptography;
using System.Text;

namespace GoldSrc.GameUI;

public sealed class ContentControlDialog : Frame
{
	private const string GorePasswordKey = "User Token 2";

	private readonly Label _status;
	private readonly Label _passwordLabel;
	private readonly Label _password2Label;
	private readonly Label _explain;
	private readonly TextEntry _password;
	private readonly TextEntry _password2;

	private string _gorePassword;

	public ContentControlDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "ContentControlDialog" )
	{
		SetBounds( 0, 0, 372, 160 );
		SetSizeable( false );
		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );

		BasePanel.PositionDialog( this );

		SetTitle( "#GameUI_ContentLock" );

		_status = new Label( vgui, this, "ContentStatus", "" );
		_passwordLabel = new Label( vgui, this, "PasswordPrompt", "#GameUI_PasswordPrompt" );
		_password2Label = new Label( vgui, this, "PasswordReentryPrompt", "#GameUI_PasswordReentryPrompt" );
		_explain = new Label( vgui, this, "ContentControlExplain", "" );
		_password = new TextEntry( vgui, this, "Password" );
		_password2 = new TextEntry( vgui, this, "Password2" );

		new Button( vgui, this, "Ok", "#GameUI_OK" ).SetCommand( "Ok" );
		new Button( vgui, this, "Cancel", "#GameUI_Cancel" ).SetCommand( "Cancel" );

		_gorePassword = Registry.ReadString( GorePasswordKey );

		LoadControlSettings( "Resource\\ContentControlDialog.res" );
	}

	public bool IsPasswordEnabledInDialog => _gorePassword.Length != 0;

	public void ResetPassword() => _gorePassword = Registry.ReadString( GorePasswordKey );

	public void ApplyPassword()
	{
		Registry.WriteString( GorePasswordKey, _gorePassword );

		UpdateContentControlStatus();
	}

	private void UpdateContentControlStatus()
	{
		var enabled = IsPasswordEnabledInDialog;

		_status.SetText( enabled ? "#GameUI_ContentStatusEnabled" : "#GameUI_ContentStatusDisabled" );
		_passwordLabel.SetText( enabled ? "#GameUI_PasswordDisablePrompt" : "#GameUI_PasswordPrompt" );

		_password2Label.SetVisible( !enabled );
		_password2.SetVisible( !enabled );
	}

	private static string HashPassword( string password )
	{
		var builder = new StringBuilder();

		foreach ( var value in MD5.HashData( Encoding.Latin1.GetBytes( password ) ) )
			builder.Append( value.ToString( "x2" ) );

		return builder.ToString();
	}

	private static void SetViolence( float value )
	{
		Cvar.SetValue( "violence_hblood", value );
		Cvar.SetValue( "violence_hgibs", value );
		Cvar.SetValue( "violence_ablood", value );
		Cvar.SetValue( "violence_agibs", value );
	}

	public override void Activate()
	{
		base.Activate();

		_password.SetText( "" );
		_password.RequestFocus();
		_password2.SetText( "" );
		_explain.SetText( "" );

		UpdateContentControlStatus();

		Vgui.Input.SetAppModalSurface( this );
	}

	public override void Close()
	{
		base.Close();

		PostActionSignal( new Message( "ContentControlClose" ) );
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Cancel", StringComparison.OrdinalIgnoreCase ) )
		{
			Close();
			return;
		}

		if ( !command.Equals( "Ok", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		var password = _password.Text;

		if ( !IsPasswordEnabledInDialog )
		{
			if ( password != _password2.Text )
			{
				_explain.SetText( "#GameUI_PasswordsDontMatch" );
				return;
			}

			if ( password.Length != 0 )
			{
				_gorePassword = HashPassword( password );

				SetViolence( 0f );
				Close();
				return;
			}
		}
		else if ( password.Length != 0 )
		{
			if ( !_gorePassword.Equals( HashPassword( password ), StringComparison.OrdinalIgnoreCase ) )
			{
				_explain.SetText( "#GameUI_IncorrectPassword" );
				return;
			}

			_gorePassword = "";

			SetViolence( 1f );
			Close();
			return;
		}

		_explain.SetText( "#GameUI_MustEnterPassword" );
	}
}

public sealed class OptionsSubAdvanced : PropertyPage
{
	private readonly ContentControlDialog _contentControlDialog;
	private readonly CheckButton _contentLockButton;
	private readonly CheckButton _originalModels;
	private readonly CheckButton _addonsFolder;
	private readonly CvarToggleCheckButton _detailTextures;

	public OptionsSubAdvanced( Vgui vgui, Panel parent ) : base( vgui, parent, null )
	{
		_contentLockButton = new CheckButton( vgui, this, "ContentlockButton", "#GameUI_ContentLock" );
		_contentLockButton.SetCommand( "ContentControl" );

		_contentControlDialog = new ContentControlDialog( vgui, parent );
		_contentControlDialog.AddActionSignalTarget( this );

		BasePanel.PositionDialog( _contentControlDialog );

		_contentLockButton.SetSelected( _contentControlDialog.IsPasswordEnabledInDialog );

		_detailTextures = new CvarToggleCheckButton( vgui, this, "DetailTextures", "#GameUI_DetailTextures", "r_detailtextures" );

		_originalModels = new CheckButton( vgui, this, "OriginalModels", "#GameUI_OriginalModels" );
		_originalModels.SetSelected( !HdModels );

		_addonsFolder = new CheckButton( vgui, this, "AddonsFolder", "#GameUI_AddonsFolder" );
		_addonsFolder.SetSelected( AddonsFolder );

		LoadControlSettings( "Resource\\OptionsSubAdvanced.res" );

		FindChildByName( "Label5" )?.SetVisible( false );

		if ( !ModInfo.DetailedTextures )
			_detailTextures.SetVisible( false );
		else if ( Client.DetailTexturesSupported <= 0f )
			_detailTextures.SetEnabled( false );
	}

	private static bool HdModels => Registry.ReadInt( "hdmodels" ) > 0;

	private static bool AddonsFolder => Registry.ReadInt( "addons_folder" ) > 0;

	protected override void OnResetData()
	{
		_contentControlDialog.ResetPassword();

		_contentLockButton.SetSelected( _contentControlDialog.IsPasswordEnabledInDialog );
		_originalModels.SetSelected( !HdModels );
		_addonsFolder.SetSelected( AddonsFolder );
		_detailTextures.Reset();
	}

	protected override void OnApplyChanges()
	{
		_contentControlDialog.ApplyPassword();

		var restart = false;

		if ( HdModels == _originalModels.IsSelected )
		{
			restart = true;

			Cmd.AddText( $"_sethdmodels {(_originalModels.IsSelected ? 0 : 1)}\n" );
		}

		if ( AddonsFolder != _addonsFolder.IsSelected )
		{
			restart = true;

			Cmd.AddText( $"_setaddons_folder {(_addonsFolder.IsSelected ? 1 : 0)}\n" );
		}

		_detailTextures.ApplyChanges();

		if ( restart )
			Cmd.AddText( "_restart\n" );
	}

	private void OnCheckButtonChecked( Panel panel, bool state )
	{
		if ( panel == _originalModels )
		{
			if ( state == !HdModels )
				return;
		}
		else if ( panel == _addonsFolder )
		{
			if ( state == AddonsFolder )
				return;
		}
		else if ( panel != _detailTextures )
		{
			return;
		}

		PostActionSignal( new Message( "ApplyButtonEnable" ) );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "CheckButtonChecked":
				OnCheckButtonChecked( message.Panel, message.Int != 0 );
				break;

			case "ControlModified":
				PostActionSignal( new Message( "ApplyButtonEnable" ) );
				break;

			case "ContentControlClose":
				_contentLockButton.SetSelected( _contentControlDialog.IsPasswordEnabledInDialog );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	public override void OnCommand( string command )
	{
		if ( !command.Equals( "ContentControl", StringComparison.OrdinalIgnoreCase ) )
		{
			base.OnCommand( command );
			return;
		}

		if ( !_contentLockButton.IsSelected && !_contentControlDialog.IsPasswordEnabledInDialog )
			_contentControlDialog.ResetPassword();
		else
			_contentControlDialog.Activate();
	}
}
