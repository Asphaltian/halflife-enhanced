using GoldSrc;

namespace HalfLife;

partial class Player
{
	private bool _hasRagdoll;
	private Vector3 _ragdollOrigin;

	public override bool CanBecomeRagdoll()
	{
		if ( GameCvars.Ragdolls == 0f || !HalfLifeDll.GameRules.IsMultiplayer() )
			return false;

		var model = Info.ValueForKey( Server.GetInfoKeyBuffer( this ), "model" );

		return model.Length != 0 && ClientRagdoll.Supports( ModelCache.ForPlayer( model )?.RenderModel );
	}

	private void CreateRagdoll()
	{
		_hasRagdoll = true;
		_ragdollOrigin = Origin;

		Server.MessageBegin( MessageDest.All, UserMessages.PlayerRagdoll );
		WriteRagdoll();
		Server.MessageEnd();

		Effects |= Effects.NoDraw;
	}

	public void SendRagdoll( Player player )
	{
		if ( !_hasRagdoll )
			return;

		Server.MessageBegin( MessageDest.One, UserMessages.PlayerRagdoll, default, player );
		WriteRagdoll();
		Server.MessageEnd();
	}

	public void RemoveRagdoll()
	{
		if ( !_hasRagdoll )
			return;

		_hasRagdoll = false;

		Server.MessageBegin( MessageDest.All, UserMessages.PlayerRagdollRemove );
		Server.WriteByte( Index );
		Server.MessageEnd();
	}

	private void WriteRagdoll()
	{
		Server.WriteByte( Index );
		Server.WriteCoord( _ragdollOrigin.x );
		Server.WriteCoord( _ragdollOrigin.y );
		Server.WriteCoord( _ragdollOrigin.z );
	}
}
