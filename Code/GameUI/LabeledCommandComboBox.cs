using System;

namespace GoldSrc.GameUI;

public sealed class LabeledCommandComboBox : ComboBox
{
	private readonly record struct CommandItem( string Name, string Command, int ComboBoxId );

	private readonly List<CommandItem> _items = [];

	private int _currentSelection = -1;
	private int _startSelection = -1;

	public LabeledCommandComboBox( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name, 6, false )
	{
		AddActionSignalTarget( this );
	}

	public void AddItem( string text, string engineCommand )
	{
		_items.Add( new CommandItem( Vgui.Localize( text ), engineCommand, AddItem( text ) ) );
	}

	public bool IsItemPresent( string text )
	{
		foreach ( var item in _items )
		{
			if ( item.Name == text )
				return true;
		}

		return false;
	}

	public string GetActiveItemCommand() => _currentSelection == -1 ? null : _items[_currentSelection].Command;

	public override void ActivateItem( int index )
	{
		if ( index >= _items.Count )
			return;

		base.ActivateItem( _items[index].ComboBoxId );

		_currentSelection = index;
	}

	public void SetInitialItem( int index )
	{
		if ( index >= _items.Count )
			return;

		_startSelection = index;

		ActivateItem( _items[index].ComboBoxId );
	}

	public void Reset()
	{
		if ( _startSelection != -1 )
			ActivateItem( _startSelection );
	}

	public void ApplyChanges()
	{
		if ( _currentSelection == -1 || _items.Count <= 0 )
			return;

		Client.ClientCmd( _items[_currentSelection].Command );

		_startSelection = _currentSelection;
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "TextChanged" )
		{
			OnTextChanged( message.Text );
			return;
		}

		base.OnMessage( message, from );
	}

	private void OnTextChanged( string text )
	{
		for ( var i = 0; i < _items.Count; i++ )
		{
			if ( !_items[i].Name.Equals( text, StringComparison.OrdinalIgnoreCase ) )
				continue;

			_currentSelection = i;
			break;
		}

		if ( _startSelection != _currentSelection )
			PostActionSignal( new Message( "ControlModified" ) );
	}
}
