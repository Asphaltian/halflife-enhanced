namespace GoldSrc;

partial class ClientEntity
{
	private const float RagdollBoneTime = 0.1f;
	private const float PlayerRagdollBoneTime = 0.05f;

	internal ClientRagdoll CreateRagdoll( Vector3 force, int forceBone )
	{
		var client = Client.Current;

		if ( _renderer is not SkinnedModelRenderer source || (Model ?? client.GetModelByIndex( ModelIndex )) is not StudioModel { File: { } header } )
			return null;

		if ( !ClientRagdoll.Supports( source.Model ) )
			return null;

		var world = WorldTransform;
		var bones = client.BoneSetup;
		var previous = new Transform[header.Bones.Length];
		var current = new Transform[header.Bones.Length];

		bones.SetupBones( header, ref CurState, ref _latched, client.ClientTime - RagdollBoneTime, Client.StudioInterpolation, MouthOpen );
		RagdollBones( header, bones, world, previous );

		bones.SetupBones( header, ref CurState, ref _latched, client.ClientTime, Client.StudioInterpolation, MouthOpen );
		RagdollBones( header, bones, world, current );

		return CreateRagdoll( source.Model, header, world, CurState, previous, current, RagdollBoneTime, force, forceBone, false );
	}

	internal ClientRagdoll CreateLocalPlayerRagdoll( int playerIndex )
	{
		var client = Client.Current;

		if ( PlayerRagdollModel( client, playerIndex ) is not { } model )
			return null;

		StudioSetupPlayer( client, model.File, client.GetPlayerInfo( playerIndex ), ref client.CurrentFrame.PlayerState[playerIndex], Client.StudioInterpolation, Origin, Angles );

		return CreatePlayerRagdoll( client, model, playerIndex, WorldTransform );
	}

	internal ClientRagdoll CreateRemotePlayerRagdoll( int playerIndex, float targetTime )
	{
		var client = Client.Current;

		if ( PlayerRagdollModel( client, playerIndex ) is not { } model )
			return null;

		var origin = Origin;
		var angles = Angles;

		ComputePlayerOrigin( targetTime );

		var current = Origin;

		ComputePlayerOrigin( targetTime - PlayerRagdollBoneTime );

		var previousWorld = WorldTransform;

		previousWorld.Position += Origin - current;

		Origin = origin;
		Angles = angles;

		return CreatePlayerRagdoll( client, model, playerIndex, previousWorld );
	}

	internal static ClientRagdoll CreateDormantPlayerRagdoll( StudioModel model, Vector3 origin )
	{
		if ( model is not { File: { } header } || !ClientRagdoll.Supports( model.RenderModel ) )
			return null;

		var client = Client.Current;
		var bones = client.BoneSetup;
		var world = new Transform( origin );
		var pose = new Transform[header.Bones.Length];
		var latched = new Studio.LatchedVars { PrevController = [CenterController, CenterController, CenterController, CenterController] };
		var state = new EntityState
		{
			Origin = origin,
			Body = Client.HighModels != 0f ? 255 : 0,
			Controller0 = CenterController,
			Controller1 = CenterController,
			Controller2 = CenterController,
			Controller3 = CenterController
		};

		bones.SetupBones( header, ref state, ref latched, client.ClientTime, false );
		RagdollBones( header, bones, world, pose );

		return CreateRagdoll( model.RenderModel, header, world, state, pose, pose, PlayerRagdollBoneTime, default, 0, true );
	}

	private StudioModel PlayerRagdollModel( Client client, int playerIndex )
	{
		var model = client.SetupPlayerModel( playerIndex, Model ?? client.GetModelByIndex( ModelIndex ) ) as StudioModel;

		return model?.File is not null && ClientRagdoll.Supports( model.RenderModel ) ? model : null;
	}

	private ClientRagdoll CreatePlayerRagdoll( Client client, StudioModel model, int playerIndex, Transform previousWorld )
	{
		var header = model.File;
		var world = WorldTransform;
		var bones = client.BoneSetup;
		var playerInfo = client.GetPlayerInfo( playerIndex );
		var previous = new Transform[header.Bones.Length];
		var current = new Transform[header.Bones.Length];

		bones.SetupBones( header, ref CurState, ref _latched, client.ClientTime - PlayerRagdollBoneTime, Client.StudioInterpolation, MouthOpen, playerInfo );
		RagdollBones( header, bones, previousWorld, previous );

		bones.SetupBones( header, ref CurState, ref _latched, client.ClientTime, Client.StudioInterpolation, MouthOpen, playerInfo );
		RagdollBones( header, bones, world, current );

		return CreateRagdoll( model.RenderModel, header, world, CurState, previous, current, PlayerRagdollBoneTime, default, 0, true );
	}

	private static ClientRagdoll CreateRagdoll( Sandbox.Model model, Studio.File header, Transform world, in EntityState state, Transform[] previous, Transform[] current, float boneTime, Vector3 force, int forceBone, bool player )
	{
		var gameObject = new GameObject( true, "ragdoll" ) { NetworkMode = NetworkMode.Never, WorldTransform = world };
		var renderer = gameObject.AddComponent<SkinnedModelRenderer>();

		renderer.Model = model;

		Studio.BoneOutput.SetBody( header, renderer, state.Body, state.Skin );

		var ragdoll = gameObject.AddComponent<ClientRagdoll>();

		ragdoll.Init( renderer, header, (Effects)state.Effects, state.IUser4, previous, current, boneTime, force, forceBone, player );

		return ragdoll;
	}

	private static void RagdollBones( Studio.File header, Studio.BoneSetup bones, Transform world, Transform[] transforms )
	{
		for ( var i = 0; i < header.Bones.Length; i++ )
		{
			var local = new Transform( bones.Positions[i], bones.Rotations[i] );
			var parent = header.Bones[i].Parent;

			transforms[i] = parent == -1 ? world.ToWorld( local ) : transforms[parent].ToWorld( local );
		}
	}
}
