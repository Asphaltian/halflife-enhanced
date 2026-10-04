using System;
using System.Threading.Tasks;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Server
{
	private readonly Dictionary<string, SaveRestoreData> _saveDirectory = new( StringComparer.OrdinalIgnoreCase );
	private static int _changeLevelSpawnCount;

	private bool _changingLevel;

	public bool HasPendingChangeLevel => _changingLevel;

	public void ChangeLevel( string level, string startSpot )
	{
		if ( SpawnCount == _changeLevelSpawnCount )
			return;

		_changeLevelSpawnCount = SpawnCount;

		SkipUpdates();

		Cmd.AddText( startSpot is null ? $"changelevel {level}\n" : $"changelevel2 {level} {startSpot}\n" );
	}

	private void SkipUpdates()
	{
		foreach ( var client in Clients )
		{
			if ( (client.Active || client.Connected || client.Spawned) && _hostClient is not { FakeClient: true } )
				client.SkipMessage = true;
		}
	}

	public void ClearGameState()
	{
		_saveDirectory.Clear();

		GameDll ??= GameDll.Load();
		GameDll.ResetGlobalState();
	}

	public async Task ChangeLevel2( string level, string startSpot )
	{
		if ( _changingLevel )
			return;

		if ( !Active )
		{
			Log.Info( "Only the server may changelevel" );
			return;
		}

		if ( MaxClients > 1 )
		{
			Log.Info( "changelevel2 <levelname> : not for use with multiplayer games" );
			return;
		}

		_changingLevel = true;

		if ( string.IsNullOrEmpty( startSpot ) )
			startSpot = null;

		var oldLevel = Name;

		if ( Client.Current is { } client )
		{
			client.BeginLoadingPlaque();

			await client.LoadingPlaquePresented();
		}

		SaveGamestate();
		ServerDeactivate();
		Active = false;

		Client.Current?.ClearMemory();

		if ( !await SpawnServer( level, startSpot ) )
		{
			_changingLevel = false;

			Client.Current?.EndLoadingPlaque();

			throw HostError( $"Host_Changelevel2: Couldn't load map {level}\n" );
		}

		Globals.SaveData = null;

		if ( !LoadGamestate( level, false ) )
		{
			LoadEntities();
			LoadAdjacentEntities( oldLevel, startSpot );
			Globals.Time = (float)Time;

			if ( SvNewUnit != 0f )
				_saveDirectory.Clear();
		}
		else
		{
			LoadAdjacentEntities( oldLevel, startSpot );
			Globals.Time = (float)Time;
		}

		SetLoadGame();
		ActivateServer( false );

		_changingLevel = false;
	}

	private SaveRestoreData SaveGamestate()
	{
		var data = new SaveRestoreData
		{
			Table = new EntityTable[_numEdicts],
			Time = Globals.Time
		};

		for ( var i = 0; i < _numEdicts; i++ )
			data.Table[i] = new EntityTable { Id = i, Edict = _edicts[i] };

		Globals.SaveData = data;

		GameDll.ParmsChangeLevel( this );

		data.CurrentMapName = Name;
		data.Time = (float)Time;
		data.LightStyles = [.. LightStyles];
		data.Skill = (int)Skill;
		data.SkyName = SvSkyName;
		data.Sky = [(int)SvSkyColorR, (int)SvSkyColorG, (int)SvSkyColorB, SvSkyVecX, SvSkyVecY, SvSkyVecZ];

		for ( var i = 0; i < _numEdicts; i++ )
		{
			data.CurrentIndex = i;
			data.Table[i].Blocks = null;

			if ( _edicts[i] is not { } edict )
				continue;

			edict.DispatchSave( data );

			if ( i > 0 && i < MaxClients + 1 )
				data.Table[i].Flags |= EntityTable.Player;
		}

		data.Decals = Client.Current?.DecalListCreate();

		_saveDirectory[Name] = data;

		return data;
	}

	private SaveRestoreData LoadSaveData( string level )
	{
		if ( !_saveDirectory.TryGetValue( level, out var saved ) )
		{
			Log.Trace( $"Couldn't open save data file SAVE/{level}.HL1." );
			return null;
		}

		var data = new SaveRestoreData
		{
			Table = new EntityTable[saved.Table.Length],
			CurrentMapName = level,
			Time = saved.Time,
			UseLandmark = true
		};

		for ( var i = 0; i < saved.Table.Length; i++ )
		{
			var entry = saved.Table[i];

			data.Table[i] = new EntityTable
			{
				Id = entry.Id,
				Flags = (entry.Flags & EntityTable.Removed) != 0 ? EntityTable.Removed : entry.Flags,
				ClassName = entry.ClassName,
				Blocks = entry.Blocks
			};
		}

		foreach ( var connection in saved.LevelList )
		{
			data.LevelList.Add( new LevelList
			{
				MapName = connection.MapName,
				LandmarkName = connection.LandmarkName,
				LandmarkOrigin = connection.LandmarkOrigin
			} );
		}

		Globals.SaveData = data;

		return data;
	}

	private void EntityPatchWrite( SaveRestoreData data, string level )
	{
		var saved = _saveDirectory[level];

		for ( var i = 0; i < data.Table.Length; i++ )
		{
			if ( (data.Table[i].Flags & EntityTable.Removed) != 0 )
				saved.Table[i].Flags = EntityTable.Removed;
		}
	}

	private const int RestoredLightStyleLength = 3;

	public bool LoadGamestate( string level, bool createPlayers )
	{
		var data = LoadSaveData( level );
		if ( data is null )
			return false;

		var saved = _saveDirectory[level];

		Array.Clear( LightStyles );

		for ( var i = 0; i < saved.LightStyles.Length && i < MaxLightStyles; i++ )
		{
			if ( saved.LightStyles[i] is { Length: > 0 } style )
				LightStyles[i] = style.Length > RestoredLightStyleLength ? style[..RestoredLightStyleLength] : style;
		}

		Cvar.SetValue( "skill", saved.Skill );
		Cvar.Set( "sv_skyname", saved.SkyName );
		Cvar.SetValue( "sv_skycolor_r", saved.Sky[0] );
		Cvar.SetValue( "sv_skycolor_g", saved.Sky[1] );
		Cvar.SetValue( "sv_skycolor_b", saved.Sky[2] );
		Cvar.SetValue( "sv_skyvec_x", saved.Sky[3] );
		Cvar.SetValue( "sv_skyvec_y", saved.Sky[4] );
		Cvar.SetValue( "sv_skyvec_z", saved.Sky[5] );

		for ( var i = 0; i < data.Table.Length; i++ )
		{
			var entry = data.Table[i];

			if ( entry.ClassName is null || entry.Blocks is null || (entry.Flags & EntityTable.Removed) != 0 )
			{
				entry.Edict = null;
			}
			else if ( entry.Id == 0 )
			{
				entry.Edict = CreateEdict( 0, entry.ClassName );
			}
			else if ( entry.Id < MaxClients + 1 )
			{
				if ( (entry.Flags & EntityTable.Player) == 0 )
					throw new InvalidOperationException( $"ENTITY IS NOT A PLAYER: {i}\n" );

				entry.Edict = createPlayers ? CreateEdict( entry.Id, entry.ClassName ) : null;
			}
			else
			{
				entry.Edict = CreateNamedEntity( entry.ClassName );
			}
		}

		for ( var i = 0; i < data.Table.Length; i++ )
		{
			var entry = data.Table[i];
			data.CurrentIndex = i;

			if ( entry.Edict is not { } edict )
				continue;

			if ( edict.DispatchRestore( data, false ) < 0 )
			{
				entry.Edict = null;
				FreeEdict( edict );
			}
			else
			{
				LinkEdict( edict, false );
			}
		}

		Globals.SaveData = null;
		Time = data.Time;

		return true;
	}

	private void LoadAdjacentEntities( string oldLevel, string landmarkName )
	{
		var current = new SaveRestoreData();

		Globals.SaveData = current;
		GameDll.ParmsChangeLevel( this );

		for ( var i = 0; i < current.LevelList.Count; i++ )
		{
			var map = current.LevelList[i].MapName;
			var seen = false;

			for ( var test = 0; test < i && !seen; test++ )
				seen = current.LevelList[test].MapName == map;

			if ( seen )
				continue;

			var data = LoadSaveData( map );
			if ( data is null )
				continue;

			data.Time = (float)Time;
			data.UseLandmark = true;
			data.LandmarkName = landmarkName;

			var landmarkOrigin = current.LevelList.Find( x => x.LandmarkName == landmarkName )?.LandmarkOrigin ?? Vector3.Zero;
			var savedOrigin = data.LevelList.Find( x => x.LandmarkName == landmarkName )?.LandmarkOrigin ?? Vector3.Zero;

			data.LandmarkOffset = landmarkOrigin - savedOrigin;

			var flags = map == oldLevel ? EntityTable.Player : 0;

			for ( var index = 0; index < data.LevelList.Count; index++ )
			{
				if ( data.LevelList[index].MapName == Name )
					flags |= 1 << index;
			}

			if ( flags != 0 && CreateEntityList( data, flags ) != 0 )
				EntityPatchWrite( data, map );

			Globals.SaveData = null;
		}

		Globals.SaveData = null;
	}

	private int CreateEntityList( SaveRestoreData data, int levelMask )
	{
		var movedCount = 0;

		for ( var i = 0; i < data.Table.Length; i++ )
		{
			var entry = data.Table[i];
			Edict edict = null;

			if ( entry.ClassName is not null && entry.Blocks is not null && entry.Id > 0 )
			{
				var active = (entry.Flags & levelMask) != 0;

				if ( entry.Id < MaxClients + 1 )
				{
					if ( active )
					{
						if ( (entry.Flags & EntityTable.Player) == 0 )
							throw new InvalidOperationException( $"ENTITY IS NOT A PLAYER: {i}\n" );

						edict = CreateEdict( entry.Id, entry.ClassName );
					}
					else
					{
						edict = GetEdict( entry.Id );
					}
				}
				else if ( active )
				{
					edict = CreateNamedEntity( entry.ClassName );
				}
			}

			entry.Edict = edict;
		}

		for ( var i = 0; i < data.Table.Length; i++ )
		{
			var entry = data.Table[i];
			data.CurrentIndex = i;

			if ( entry.Edict is not { } edict || (entry.Flags & levelMask) == 0 )
				continue;

			if ( (entry.Flags & EntityTable.Global) != 0 )
			{
				Log.Trace( $"Merging changes for global: {entry.ClassName}" );

				edict.DispatchRestore( data, true );
				FreeEdict( edict );
				continue;
			}

			Log.Trace( $"Transferring {entry.ClassName} ({edict.Index})" );

			if ( edict.DispatchRestore( data, false ) < 0 )
			{
				FreeEdict( edict );
				continue;
			}

			LinkEdict( edict, false );

			var followsClient = edict.MoveType == MoveType.Follow && edict.AimEntity is { } aim && (aim.EntityFlags & EntityFlags.Client) == EntityFlags.Client;

			if ( (entry.Flags & EntityTable.Player) == 0 && !followsClient
				&& PointContents( (edict.AbsMin + edict.AbsMax) * 0.5f ) == (int)Contents.Solid )
			{
				Log.Trace( $"Suppressing {entry.ClassName}" );
				FreeEdict( edict );
				continue;
			}

			movedCount++;
			entry.Flags = EntityTable.Removed;
		}

		return movedCount;
	}
}
