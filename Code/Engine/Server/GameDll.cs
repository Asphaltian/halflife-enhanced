using System;

namespace GoldSrc;

public abstract class GameDll
{
	public abstract void Init();

	public abstract void Move( PlayerMove pmove, bool server );

	public abstract void ClientKill( Edict client );

	public abstract bool ClientConnect( Edict entity, string name, string address, ref string rejectReason );

	public abstract void ClientDisconnect( Edict entity );

	public abstract void ClientPutInServer( Edict client );

	public abstract void PlayerCustomization( Edict entity, Customization customization );

	public abstract void ClientCommand( Edict client );

	public abstract void ClientUserInfoChanged( Edict client, ref string info );

	public abstract void CmdStart( Edict player, UserCmd cmd, int randomSeed );

	public abstract void ServerActivate( Server server );

	public abstract void ServerDeactivate();

	public abstract void StartFrame( Server server );

	public abstract byte[] SetupVisibility( Edict viewEntity, Edict client );

	public abstract bool AddToFullPack( ref EntityState state, int index, Edict entity, Edict host, int hostFlags, bool player, byte[] set );

	public abstract void CreateBaseline( bool player, int index, ref EntityState baseline, Edict entity, int playerModelIndex, Vector3 playerMins, Vector3 playerMaxs );

	public abstract void CreateInstancedBaselines();

	public abstract void UpdateClientData( Edict entity, bool sendWeapons, ref LocalClientData data );

	public abstract bool GetWeaponData( Edict player, WeaponData[] info );

	public abstract void RegisterEncoders();

	public abstract void SaveGlobalState( List<SaveBlock> blocks );

	public abstract void RestoreGlobalState( List<SaveBlock> blocks );

	public abstract void ResetGlobalState();

	public abstract void ParmsChangeLevel( Server server );

	public abstract bool AllowLagCompensation();

	public abstract string GetGameDescription();

	public static GameDll Load()
	{
		foreach ( var type in Game.TypeLibrary.GetTypes<GameDll>() )
		{
			if ( type.IsAbstract )
				continue;

			var dll = type.Create<GameDll>();

			dll.Init();

			return dll;
		}

		throw new InvalidOperationException( "Unable to load game DLL" );
	}
}
