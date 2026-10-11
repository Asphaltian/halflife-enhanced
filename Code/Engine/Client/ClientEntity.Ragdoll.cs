namespace GoldSrc;

partial class ClientEntity
{
	private const float RagdollBoneTime = 0.1f;

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

		var gameObject = new GameObject( true, "ragdoll" ) { NetworkMode = NetworkMode.Never, WorldTransform = world };
		var renderer = gameObject.AddComponent<SkinnedModelRenderer>();

		renderer.Model = source.Model;

		Studio.BoneOutput.SetBody( header, renderer, CurState.Body, CurState.Skin );

		var ragdoll = gameObject.AddComponent<ClientRagdoll>();

		ragdoll.Init( renderer, header, (Effects)CurState.Effects, CurState.IUser4, previous, current, RagdollBoneTime, force, forceBone );

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
