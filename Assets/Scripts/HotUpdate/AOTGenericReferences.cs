using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"MemoryPack.Core.dll",
		"System.Core.dll",
		"System.Runtime.CompilerServices.Unsafe.dll",
		"System.dll",
		"UniTask.dll",
		"Unity.Burst.dll",
		"Unity.Collections.dll",
		"Unity.Entities.dll",
		"Unity.InferenceEngine.dll",
		"UnityEngine.CoreModule.dll",
		"UnityEngine.JSONSerializeModule.dll",
		"UnityEngine.UIElementsModule.dll",
		"YooAsset.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__10,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.AbandonHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Attribute.Handlers.ScryCardsHandler.<ExecuteAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CostCompensationService.<ExecutePayloadAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.CostCompensationService.<PayWithCompensationAsync>d__0,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.DelayedEffectScheduler.<Fire>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteFlatAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.EffectExecutor.<ExecuteStepsAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ActivateHeroSkill>d__2,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveCardCastAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<ResolveSpellEffectsAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameActions.<SettleResponseWindowAsync>d__19>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameCore.<EnforceHandLimitAsync>d__56>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameLoopController.<ProcessPriority>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.GameLoopController.<ResolveStack>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_1>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_3>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<ActivateAsync>d__15,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<DiscoverFromDeckAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<GraveyardCreatureToLandTappedAsync>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.HeroSkillSystem.<RecycleFromGraveyardAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.LockRevealedAura.<PromptLockAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.ProphecySystem.<ResolveAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.SkipStandbyCountTracker.<PromptSkipStandbyAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<BeginResolution>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<FinishResolution>d__50>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<PlayerPass>d__45>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<ResolveStack>d__49>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.StackEngine.<ResolveTop>d__55>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.BattleController.<RunAiTurnAsync>d__20>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<DoCombatAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.SimpleAI.<TakeTurnAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.UiTargetSelector.<RunCountdown>d__6>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<SynergyUI.UiTargetSelector.<SelectIndicesAsync>d__3,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__10,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.AbandonHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Attribute.Handlers.ScryCardsHandler.<ExecuteAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CostCompensationService.<ExecutePayloadAsync>d__3>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.CostCompensationService.<PayWithCompensationAsync>d__0,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.DelayedEffectScheduler.<Fire>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteFlatAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.EffectExecutor.<ExecuteStepsAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ActivateHeroSkill>d__2,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveCardCastAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<ResolveSpellEffectsAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameActions.<SettleResponseWindowAsync>d__19>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameCore.<EnforceHandLimitAsync>d__56>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameLoopController.<ProcessPriority>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.GameLoopController.<ResolveStack>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_1>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_3>d>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<ActivateAsync>d__15,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<DiscoverFromDeckAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<GraveyardCreatureToLandTappedAsync>d__18>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.HeroSkillSystem.<RecycleFromGraveyardAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.LockRevealedAura.<PromptLockAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.ProphecySystem.<ResolveAsync>d__10>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.SkipStandbyCountTracker.<PromptSkipStandbyAsync>d__9>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<BeginResolution>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<FinishResolution>d__50>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<PlayerPass>d__45>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<ResolveStack>d__49>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.StackEngine.<ResolveTop>d__55>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestAsync>d__13,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14,int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.BattleController.<RunAiTurnAsync>d__20>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<DoCombatAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.SimpleAI.<TakeTurnAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.UiTargetSelector.<RunCountdown>d__6>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<SynergyUI.UiTargetSelector.<SelectIndicesAsync>d__3,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<ActivateSkillAsync>d__67>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<DrainNetSelects>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<SynergyUI.BattleScreen.<StartNetAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<ActivateSkillAsync>d__67>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<DrainNetSelects>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<SynergyUI.BattleScreen.<StartNetAsync>d__27>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<byte>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<int>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<object>
	// Cysharp.Threading.Tasks.ITaskPoolNode<object>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<byte>
	// Cysharp.Threading.Tasks.IUniTaskSource<int>
	// Cysharp.Threading.Tasks.IUniTaskSource<object>
	// Cysharp.Threading.Tasks.Internal.StatePool<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>
	// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<byte>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<int>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<object>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<byte>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<int>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<object>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<byte>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<int>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<object>
	// Cysharp.Threading.Tasks.UniTask.WhenAnyPromise.<>c<object,object>
	// Cysharp.Threading.Tasks.UniTask.WhenAnyPromise<object,object>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,int>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTask<byte>
	// Cysharp.Threading.Tasks.UniTask<int>
	// Cysharp.Threading.Tasks.UniTask<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<System.ValueTuple<int,object,object>>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<byte>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<int>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<object>
	// Cysharp.Threading.Tasks.UniTaskExtensions.<>c__51<byte>
	// MemoryPack.Formatters.ArrayFormatter<byte>
	// MemoryPack.Formatters.ArrayFormatter<int>
	// MemoryPack.Formatters.ArrayFormatter<object>
	// MemoryPack.Formatters.ArrayFormatter<uint>
	// MemoryPack.Formatters.NullableFormatter<int>
	// MemoryPack.Formatters.UnmanagedFormatter<int>
	// MemoryPack.IMemoryPackFormatter<object>
	// MemoryPack.IMemoryPackable<object>
	// MemoryPack.MemoryPackFormatter<System.Nullable<int>>
	// MemoryPack.MemoryPackFormatter<int>
	// MemoryPack.MemoryPackFormatter<object>
	// System.Action<CardCore.AI.NeuralEnv.TideAction>
	// System.Action<CardCore.EventManager.SubscriptionEntry>
	// System.Action<HexMap.HexPoiData>
	// System.Action<MemoryPack.Internal.BufferSegment>
	// System.Action<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Action<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Action<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Action<System.ValueTuple<int,float,float>>
	// System.Action<System.ValueTuple<int,object>>
	// System.Action<System.ValueTuple<object,int>>
	// System.Action<System.ValueTuple<object,object>>
	// System.Action<UnityEngine.UIElements.RuleMatcher>
	// System.Action<byte>
	// System.Action<float>
	// System.Action<int,int>
	// System.Action<int>
	// System.Action<object,object>
	// System.Action<object>
	// System.Action<ulong>
	// System.ArraySegment.Enumerator<Unity.Mathematics.int2>
	// System.ArraySegment.Enumerator<byte>
	// System.ArraySegment.Enumerator<float>
	// System.ArraySegment.Enumerator<int>
	// System.ArraySegment.Enumerator<object>
	// System.ArraySegment.Enumerator<uint>
	// System.ArraySegment.Enumerator<ushort>
	// System.ArraySegment<Unity.Mathematics.int2>
	// System.ArraySegment<byte>
	// System.ArraySegment<float>
	// System.ArraySegment<int>
	// System.ArraySegment<object>
	// System.ArraySegment<uint>
	// System.ArraySegment<ushort>
	// System.Buffers.ArrayPool<byte>
	// System.Buffers.IBufferWriter<byte>
	// System.Buffers.TlsOverPerCoreLockedStacksArrayPool.LockedStack<byte>
	// System.Buffers.TlsOverPerCoreLockedStacksArrayPool.PerCoreLockedStacks<byte>
	// System.Buffers.TlsOverPerCoreLockedStacksArrayPool<byte>
	// System.ByReference<Unity.Mathematics.int2>
	// System.ByReference<byte>
	// System.ByReference<float>
	// System.ByReference<int>
	// System.ByReference<object>
	// System.ByReference<uint>
	// System.ByReference<ushort>
	// System.Collections.Concurrent.ConcurrentDictionary.<GetEnumerator>d__35<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.DictionaryEnumerator<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.Node<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.Tables<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary<object,object>
	// System.Collections.Concurrent.ConcurrentQueue.<Enumerate>d__28<object>
	// System.Collections.Concurrent.ConcurrentQueue.Segment<object>
	// System.Collections.Concurrent.ConcurrentQueue<object>
	// System.Collections.Generic.ArraySortHelper<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ArraySortHelper<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ArraySortHelper<HexMap.HexPoiData>
	// System.Collections.Generic.ArraySortHelper<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ArraySortHelper<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<int,object>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object>>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ArraySortHelper<byte>
	// System.Collections.Generic.ArraySortHelper<float>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.ArraySortHelper<ulong>
	// System.Collections.Generic.Comparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.Comparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.Comparer<HexMap.HexPoiData>
	// System.Collections.Generic.Comparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.Comparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.Comparer<System.Guid>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.Comparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.Comparer<byte>
	// System.Collections.Generic.Comparer<float>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Comparer<uint>
	// System.Collections.Generic.Comparer<ulong>
	// System.Collections.Generic.ComparisonComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ComparisonComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ComparisonComparer<HexMap.HexPoiData>
	// System.Collections.Generic.ComparisonComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ComparisonComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ComparisonComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ComparisonComparer<System.Guid>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ComparisonComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ComparisonComparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ComparisonComparer<byte>
	// System.Collections.Generic.ComparisonComparer<float>
	// System.Collections.Generic.ComparisonComparer<int>
	// System.Collections.Generic.ComparisonComparer<object>
	// System.Collections.Generic.ComparisonComparer<uint>
	// System.Collections.Generic.ComparisonComparer<ulong>
	// System.Collections.Generic.Dictionary.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.KeyCollection<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,float>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,byte>
	// System.Collections.Generic.Dictionary.KeyCollection<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<uint,object>
	// System.Collections.Generic.Dictionary.KeyCollection<ulong,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,float>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,byte>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<uint,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<ulong,int>
	// System.Collections.Generic.Dictionary.ValueCollection<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,float>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,byte>
	// System.Collections.Generic.Dictionary.ValueCollection<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<uint,object>
	// System.Collections.Generic.Dictionary.ValueCollection<ulong,int>
	// System.Collections.Generic.Dictionary<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.Dictionary<int,float>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<long,object>
	// System.Collections.Generic.Dictionary<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.Dictionary<object,byte>
	// System.Collections.Generic.Dictionary<object,int>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.Dictionary<uint,object>
	// System.Collections.Generic.Dictionary<ulong,int>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.EqualityComparer<byte>
	// System.Collections.Generic.EqualityComparer<float>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<long>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.EqualityComparer<uint>
	// System.Collections.Generic.EqualityComparer<ulong>
	// System.Collections.Generic.HashSet.Enumerator<int>
	// System.Collections.Generic.HashSet.Enumerator<object>
	// System.Collections.Generic.HashSet.Enumerator<uint>
	// System.Collections.Generic.HashSet<int>
	// System.Collections.Generic.HashSet<object>
	// System.Collections.Generic.HashSet<uint>
	// System.Collections.Generic.HashSetEqualityComparer<int>
	// System.Collections.Generic.HashSetEqualityComparer<object>
	// System.Collections.Generic.HashSetEqualityComparer<uint>
	// System.Collections.Generic.ICollection<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ICollection<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ICollection<HexMap.HexPoiData>
	// System.Collections.Generic.ICollection<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ICollection<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ICollection<System.ValueTuple<int,object>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,int>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object>>
	// System.Collections.Generic.ICollection<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ICollection<byte>
	// System.Collections.Generic.ICollection<float>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.ICollection<uint>
	// System.Collections.Generic.ICollection<ulong>
	// System.Collections.Generic.IComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IComparer<HexMap.HexPoiData>
	// System.Collections.Generic.IComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IComparer<System.Guid>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.IComparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IComparer<byte>
	// System.Collections.Generic.IComparer<float>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IComparer<uint>
	// System.Collections.Generic.IComparer<ulong>
	// System.Collections.Generic.IDictionary<object,object>
	// System.Collections.Generic.IEnumerable<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IEnumerable<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IEnumerable<HexMap.HexPoiData>
	// System.Collections.Generic.IEnumerable<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IEnumerable<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<int,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerable<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IEnumerable<byte>
	// System.Collections.Generic.IEnumerable<float>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerable<uint>
	// System.Collections.Generic.IEnumerable<ulong>
	// System.Collections.Generic.IEnumerator<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IEnumerator<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IEnumerator<HexMap.HexPoiData>
	// System.Collections.Generic.IEnumerator<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IEnumerator<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,byte>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<uint,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<ulong,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<int,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,int>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerator<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IEnumerator<byte>
	// System.Collections.Generic.IEnumerator<float>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEnumerator<uint>
	// System.Collections.Generic.IEnumerator<ulong>
	// System.Collections.Generic.IEqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<long>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IEqualityComparer<uint>
	// System.Collections.Generic.IEqualityComparer<ulong>
	// System.Collections.Generic.IList<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.IList<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.IList<HexMap.HexPoiData>
	// System.Collections.Generic.IList<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.IList<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.IList<System.ValueTuple<int,object>>
	// System.Collections.Generic.IList<System.ValueTuple<object,int>>
	// System.Collections.Generic.IList<System.ValueTuple<object,object>>
	// System.Collections.Generic.IList<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.IList<byte>
	// System.Collections.Generic.IList<float>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IList<ulong>
	// System.Collections.Generic.IReadOnlyCollection<System.ValueTuple<int,int>>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyDictionary<int,float>
	// System.Collections.Generic.IReadOnlyDictionary<object,int>
	// System.Collections.Generic.IReadOnlyList<System.ValueTuple<int,int>>
	// System.Collections.Generic.IReadOnlyList<object>
	// System.Collections.Generic.KeyValuePair<System.ValueTuple<int,byte>,object>
	// System.Collections.Generic.KeyValuePair<int,float>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<long,object>
	// System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,int>>
	// System.Collections.Generic.KeyValuePair<object,byte>
	// System.Collections.Generic.KeyValuePair<object,int>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.KeyValuePair<uint,object>
	// System.Collections.Generic.KeyValuePair<ulong,int>
	// System.Collections.Generic.List.Enumerator<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.List.Enumerator<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.List.Enumerator<HexMap.HexPoiData>
	// System.Collections.Generic.List.Enumerator<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.List.Enumerator<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<int,object>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,int>>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.List.Enumerator<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.List.Enumerator<byte>
	// System.Collections.Generic.List.Enumerator<float>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List.Enumerator<ulong>
	// System.Collections.Generic.List<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.List<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.List<HexMap.HexPoiData>
	// System.Collections.Generic.List<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.List<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.List<System.ValueTuple<int,object>>
	// System.Collections.Generic.List<System.ValueTuple<object,int>>
	// System.Collections.Generic.List<System.ValueTuple<object,object>>
	// System.Collections.Generic.List<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.List<byte>
	// System.Collections.Generic.List<float>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.List<ulong>
	// System.Collections.Generic.ObjectComparer<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.Generic.ObjectComparer<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.Generic.ObjectComparer<HexMap.HexPoiData>
	// System.Collections.Generic.ObjectComparer<MemoryPack.Internal.BufferSegment>
	// System.Collections.Generic.ObjectComparer<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ObjectComparer<System.Guid>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,float,float>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<int,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ObjectComparer<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.Generic.ObjectComparer<byte>
	// System.Collections.Generic.ObjectComparer<float>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectComparer<uint>
	// System.Collections.Generic.ObjectComparer<ulong>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,int>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<int,object,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,int>>
	// System.Collections.Generic.ObjectEqualityComparer<byte>
	// System.Collections.Generic.ObjectEqualityComparer<float>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<long>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<uint>
	// System.Collections.Generic.ObjectEqualityComparer<ulong>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<object>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass52_0<ulong>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass53_0<ulong>
	// System.Collections.Generic.SortedSet.<>c__DisplayClass85_0<ulong>
	// System.Collections.Generic.SortedSet.<Reverse>d__94<ulong>
	// System.Collections.Generic.SortedSet.Enumerator<ulong>
	// System.Collections.Generic.SortedSet.Node<ulong>
	// System.Collections.Generic.SortedSet.TreeSubSet.<>c__DisplayClass9_0<ulong>
	// System.Collections.Generic.SortedSet.TreeSubSet<ulong>
	// System.Collections.Generic.SortedSet<ulong>
	// System.Collections.Generic.SortedSetEqualityComparer<ulong>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<object>
	// System.Collections.Generic.TreeWalkPredicate<ulong>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.AI.NeuralEnv.TideAction>
	// System.Collections.ObjectModel.ReadOnlyCollection<CardCore.EventManager.SubscriptionEntry>
	// System.Collections.ObjectModel.ReadOnlyCollection<HexMap.HexPoiData>
	// System.Collections.ObjectModel.ReadOnlyCollection<MemoryPack.Internal.BufferSegment>
	// System.Collections.ObjectModel.ReadOnlyCollection<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,float,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<int,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.UIElements.RuleMatcher>
	// System.Collections.ObjectModel.ReadOnlyCollection<byte>
	// System.Collections.ObjectModel.ReadOnlyCollection<float>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<ulong>
	// System.Comparison<CardCore.AI.NeuralEnv.TideAction>
	// System.Comparison<CardCore.EventManager.SubscriptionEntry>
	// System.Comparison<HexMap.HexPoiData>
	// System.Comparison<MemoryPack.Internal.BufferSegment>
	// System.Comparison<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Comparison<System.Guid>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Comparison<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Comparison<System.ValueTuple<byte,byte>>
	// System.Comparison<System.ValueTuple<byte,int>>
	// System.Comparison<System.ValueTuple<byte,object>>
	// System.Comparison<System.ValueTuple<int,float,float>>
	// System.Comparison<System.ValueTuple<int,object,object>>
	// System.Comparison<System.ValueTuple<int,object>>
	// System.Comparison<System.ValueTuple<object,int>>
	// System.Comparison<System.ValueTuple<object,object>>
	// System.Comparison<UnityEngine.UIElements.RuleMatcher>
	// System.Comparison<byte>
	// System.Comparison<float>
	// System.Comparison<int>
	// System.Comparison<object>
	// System.Comparison<uint>
	// System.Comparison<ulong>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>
	// System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,float>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<int,object>,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Func<System.Collections.Generic.KeyValuePair<int,object>,int>
	// System.Func<System.Collections.Generic.KeyValuePair<object,int>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Func<System.ValueTuple<byte,byte>>
	// System.Func<System.ValueTuple<byte,int>>
	// System.Func<System.ValueTuple<byte,object>>
	// System.Func<System.ValueTuple<int,float,float>,float>
	// System.Func<System.ValueTuple<int,float,float>,int>
	// System.Func<System.ValueTuple<int,object,object>>
	// System.Func<System.ValueTuple<int,object>,byte>
	// System.Func<System.ValueTuple<int,object>,object>
	// System.Func<System.ValueTuple<object,int>,byte>
	// System.Func<System.ValueTuple<object,int>,object>
	// System.Func<System.ValueTuple<object,object>,byte>
	// System.Func<System.ValueTuple<object,object>,object>
	// System.Func<byte,byte>
	// System.Func<byte>
	// System.Func<float,byte>
	// System.Func<float,float>
	// System.Func<int,byte>
	// System.Func<int,int,object>
	// System.Func<int,int>
	// System.Func<int,object>
	// System.Func<int>
	// System.Func<object,Cysharp.Threading.Tasks.UniTask>
	// System.Func<object,System.DateTime>
	// System.Func<object,System.Guid>
	// System.Func<object,System.Nullable<System.ValueTuple<int,int>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Func<object,System.ValueTuple<byte,byte>>
	// System.Func<object,System.ValueTuple<byte,int>>
	// System.Func<object,System.ValueTuple<byte,object>>
	// System.Func<object,System.ValueTuple<int,object,object>>
	// System.Func<object,byte>
	// System.Func<object,float>
	// System.Func<object,int>
	// System.Func<object,object,Cysharp.Threading.Tasks.UniTask<object>>
	// System.Func<object,object,Cysharp.Threading.Tasks.UniTask>
	// System.Func<object,object,object,object>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object,uint>
	// System.Func<object>
	// System.Func<uint,byte>
	// System.IComparable<CardCore.TimestampInfo>
	// System.IEquatable<CardCore.ColorIdentity>
	// System.IEquatable<CardCore.TimestampInfo>
	// System.IEquatable<Unity.Mathematics.int2>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Buffer<System.ValueTuple<int,float,float>>
	// System.Linq.Buffer<int>
	// System.Linq.Buffer<object>
	// System.Linq.Buffer<uint>
	// System.Linq.Enumerable.<CastIterator>d__99<object>
	// System.Linq.Enumerable.<DistinctIterator>d__68<int>
	// System.Linq.Enumerable.<DistinctIterator>d__68<object>
	// System.Linq.Enumerable.<ExceptIterator>d__77<int>
	// System.Linq.Enumerable.<OfTypeIterator>d__97<object>
	// System.Linq.Enumerable.<ReverseIterator>d__79<object>
	// System.Linq.Enumerable.<SelectManyIterator>d__17<object,object>
	// System.Linq.Enumerable.<SkipIterator>d__31<int>
	// System.Linq.Enumerable.<TakeIterator>d__25<int>
	// System.Linq.Enumerable.<TakeIterator>d__25<object>
	// System.Linq.Enumerable.<TakeLastIterator>d__218<object>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<int,object>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,int>>
	// System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>
	// System.Linq.Enumerable.Iterator<float>
	// System.Linq.Enumerable.Iterator<int>
	// System.Linq.Enumerable.Iterator<object>
	// System.Linq.Enumerable.Iterator<uint>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereArrayIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereArrayIterator<int>
	// System.Linq.Enumerable.WhereArrayIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereEnumerableIterator<float>
	// System.Linq.Enumerable.WhereEnumerableIterator<int>
	// System.Linq.Enumerable.WhereEnumerableIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<uint>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Linq.Enumerable.WhereListIterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.WhereListIterator<int>
	// System.Linq.Enumerable.WhereListIterator<object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<int,object>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<float,float>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,int>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,uint>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<int,object>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<float,float>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,int>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,uint>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,float>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,int>,int>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<int,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<int,object>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.ValueTuple<object,object>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<float,float>
	// System.Linq.Enumerable.WhereSelectListIterator<int,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,int>
	// System.Linq.Enumerable.WhereSelectListIterator<object,object>
	// System.Linq.Enumerable.WhereSelectListIterator<object,uint>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>,int>
	// System.Linq.EnumerableSorter<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float,float>,float>
	// System.Linq.EnumerableSorter<System.ValueTuple<int,float,float>>
	// System.Linq.EnumerableSorter<int,int>
	// System.Linq.EnumerableSorter<int>
	// System.Linq.EnumerableSorter<object,System.Guid>
	// System.Linq.EnumerableSorter<object,byte>
	// System.Linq.EnumerableSorter<object,float>
	// System.Linq.EnumerableSorter<object,int>
	// System.Linq.EnumerableSorter<object,object>
	// System.Linq.EnumerableSorter<object,uint>
	// System.Linq.EnumerableSorter<object>
	// System.Linq.GroupedEnumerable<System.Collections.Generic.KeyValuePair<int,object>,int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.GroupedEnumerable<object,object,object>
	// System.Linq.IGrouping<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.IGrouping<object,object>
	// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>
	// System.Linq.IOrderedEnumerable<object>
	// System.Linq.IdentityFunction.<>c<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.IdentityFunction.<>c<object>
	// System.Linq.IdentityFunction<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.IdentityFunction<object>
	// System.Linq.Lookup.<GetEnumerator>d__12<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup.<GetEnumerator>d__12<object,object>
	// System.Linq.Lookup.Grouping.<GetEnumerator>d__7<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup.Grouping.<GetEnumerator>d__7<object,object>
	// System.Linq.Lookup.Grouping<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup.Grouping<object,object>
	// System.Linq.Lookup<int,System.Collections.Generic.KeyValuePair<int,object>>
	// System.Linq.Lookup<object,object>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<System.ValueTuple<int,float,float>>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<int>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<object>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>,float>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>,int>
	// System.Linq.OrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float,float>,float>
	// System.Linq.OrderedEnumerable<System.ValueTuple<int,float,float>>
	// System.Linq.OrderedEnumerable<int,int>
	// System.Linq.OrderedEnumerable<int>
	// System.Linq.OrderedEnumerable<object,System.Guid>
	// System.Linq.OrderedEnumerable<object,byte>
	// System.Linq.OrderedEnumerable<object,float>
	// System.Linq.OrderedEnumerable<object,int>
	// System.Linq.OrderedEnumerable<object,object>
	// System.Linq.OrderedEnumerable<object,uint>
	// System.Linq.OrderedEnumerable<object>
	// System.Linq.Set<int>
	// System.Linq.Set<object>
	// System.Nullable<CardCore.Attribute.MorphSnapshot>
	// System.Nullable<System.DateTime>
	// System.Nullable<System.ValueTuple<int,int>>
	// System.Nullable<byte>
	// System.Nullable<float>
	// System.Nullable<int>
	// System.Nullable<ulong>
	// System.Predicate<CardCore.AI.NeuralEnv.TideAction>
	// System.Predicate<CardCore.EventManager.SubscriptionEntry>
	// System.Predicate<HexMap.HexPoiData>
	// System.Predicate<MemoryPack.Internal.BufferSegment>
	// System.Predicate<SynergyUI.CardCostCalculator.BreakdownLine>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,float>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Predicate<System.ValueTuple<int,float,float>>
	// System.Predicate<System.ValueTuple<int,object>>
	// System.Predicate<System.ValueTuple<object,int>>
	// System.Predicate<System.ValueTuple<object,object>>
	// System.Predicate<UnityEngine.UIElements.RuleMatcher>
	// System.Predicate<byte>
	// System.Predicate<float>
	// System.Predicate<int>
	// System.Predicate<object>
	// System.Predicate<uint>
	// System.Predicate<ulong>
	// System.ReadOnlySpan.Enumerator<Unity.Mathematics.int2>
	// System.ReadOnlySpan.Enumerator<byte>
	// System.ReadOnlySpan.Enumerator<float>
	// System.ReadOnlySpan.Enumerator<int>
	// System.ReadOnlySpan.Enumerator<object>
	// System.ReadOnlySpan.Enumerator<uint>
	// System.ReadOnlySpan.Enumerator<ushort>
	// System.ReadOnlySpan<Unity.Mathematics.int2>
	// System.ReadOnlySpan<byte>
	// System.ReadOnlySpan<float>
	// System.ReadOnlySpan<int>
	// System.ReadOnlySpan<object>
	// System.ReadOnlySpan<uint>
	// System.ReadOnlySpan<ushort>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<byte>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<int>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<byte>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<int>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<int>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<byte>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<int>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<object>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<byte>
	// System.Runtime.CompilerServices.TaskAwaiter<int>
	// System.Runtime.CompilerServices.TaskAwaiter<object>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,int>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<int,object,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<int>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<object>
	// System.Span<Unity.Mathematics.int2>
	// System.Span<byte>
	// System.Span<float>
	// System.Span<int>
	// System.Span<object>
	// System.Span<uint>
	// System.Span<ushort>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<byte>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<int>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<object>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<byte>
	// System.Threading.Tasks.Sources.IValueTaskSource<int>
	// System.Threading.Tasks.Sources.IValueTaskSource<object>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Task<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.Task<byte>
	// System.Threading.Tasks.Task<int>
	// System.Threading.Tasks.Task<object>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<byte>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<int>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<object>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.TaskFactory<byte>
	// System.Threading.Tasks.TaskFactory<int>
	// System.Threading.Tasks.TaskFactory<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<byte>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<int>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<byte>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<int>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<object>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,int>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<int,object,object>>
	// System.Threading.Tasks.ValueTask<byte>
	// System.Threading.Tasks.ValueTask<int>
	// System.Threading.Tasks.ValueTask<object>
	// System.Threading.ThreadLocal.FinalizationHelper<object>
	// System.Threading.ThreadLocal.IdManager<object>
	// System.Threading.ThreadLocal.LinkedSlot<object>
	// System.Threading.ThreadLocal<object>
	// System.Tuple<object,object>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,int>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<int,object,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,byte>>
	// System.ValueTuple<byte,System.ValueTuple<byte,int>>
	// System.ValueTuple<byte,System.ValueTuple<byte,object>>
	// System.ValueTuple<byte,System.ValueTuple<int,object,object>>
	// System.ValueTuple<byte,byte>
	// System.ValueTuple<byte,int>
	// System.ValueTuple<byte,object>
	// System.ValueTuple<int,byte>
	// System.ValueTuple<int,float,float>
	// System.ValueTuple<int,float,object>
	// System.ValueTuple<int,int,int>
	// System.ValueTuple<int,int>
	// System.ValueTuple<int,object,object>
	// System.ValueTuple<int,object>
	// System.ValueTuple<object,int>
	// System.ValueTuple<object,object,int>
	// System.ValueTuple<object,object>
	// Unity.Burst.SharedStatic<Unity.Entities.TypeIndex>
	// Unity.Collections.LowLevel.Unsafe.HashMapHelper.Enumerator<Unity.Mathematics.int2>
	// Unity.Collections.LowLevel.Unsafe.HashMapHelper<Unity.Mathematics.int2>
	// Unity.Collections.NativeArray.Enumerator<Unity.Mathematics.int2>
	// Unity.Collections.NativeArray.Enumerator<float>
	// Unity.Collections.NativeArray.ReadOnly.Enumerator<Unity.Mathematics.int2>
	// Unity.Collections.NativeArray.ReadOnly.Enumerator<float>
	// Unity.Collections.NativeArray.ReadOnly<Unity.Mathematics.int2>
	// Unity.Collections.NativeArray.ReadOnly<float>
	// Unity.Collections.NativeArray<Unity.Mathematics.int2>
	// Unity.Collections.NativeArray<float>
	// Unity.Collections.NativeHashMap.ReadOnly<Unity.Mathematics.int2,Unity.Entities.Entity>
	// Unity.Collections.NativeHashMap<Unity.Mathematics.int2,Unity.Entities.Entity>
	// Unity.Entities.BlobAssetReference<HexMap.HexMapConfigBlob>
	// Unity.Entities.TypeManager.SharedTypeIndex<object>
	// Unity.InferenceEngine.Tensor<float>
	// UnityEngine.Awaitable.<>c<object>
	// UnityEngine.Awaitable.AwaitableAsyncMethodBuilder.IStateMachineBox<object>
	// UnityEngine.Awaitable.AwaitableAsyncMethodBuilder<object>
	// UnityEngine.Awaitable.Awaiter<object>
	// UnityEngine.Awaitable<object>
	// UnityEngine.Pool.CollectionPool.<>c<object,object>
	// UnityEngine.Pool.CollectionPool<object,object>
	// UnityEngine.UIElements.BaseField<byte>
	// UnityEngine.UIElements.BaseField<int>
	// UnityEngine.UIElements.BaseField<object>
	// UnityEngine.UIElements.BasePopupField.<>c<object,object>
	// UnityEngine.UIElements.BasePopupField.PopupTextElement<object,object>
	// UnityEngine.UIElements.BasePopupField<object,object>
	// UnityEngine.UIElements.ChangeEvent.<>c<byte>
	// UnityEngine.UIElements.ChangeEvent.<>c<float>
	// UnityEngine.UIElements.ChangeEvent.<>c<int>
	// UnityEngine.UIElements.ChangeEvent.<>c<object>
	// UnityEngine.UIElements.ChangeEvent<byte>
	// UnityEngine.UIElements.ChangeEvent<float>
	// UnityEngine.UIElements.ChangeEvent<int>
	// UnityEngine.UIElements.ChangeEvent<object>
	// UnityEngine.UIElements.CustomStyleProperty<float>
	// UnityEngine.UIElements.EventBase.<>c<object>
	// UnityEngine.UIElements.EventBase<object>
	// UnityEngine.UIElements.EventCallback<object>
	// UnityEngine.UIElements.EventCallbackFunctor<object>
	// UnityEngine.UIElements.INotifyValueChanged<float>
	// UnityEngine.UIElements.INotifyValueChanged<object>
	// UnityEngine.UIElements.MouseEventBase<object>
	// UnityEngine.UIElements.ObjectPool.<>c<object>
	// UnityEngine.UIElements.ObjectPool<object>
	// UnityEngine.UIElements.PanelChangedEventBase<object>
	// UnityEngine.UIElements.PointerEventBase<object>
	// UnityEngine.UIElements.PopupField.<>c__DisplayClass30_0<object>
	// UnityEngine.UIElements.PopupField<object>
	// UnityEngine.UIElements.StyleEnum<int>
	// UnityEngine.UIElements.UQueryState.ActionQueryMatcher<object>
	// UnityEngine.UIElements.UQueryState.Enumerator<object>
	// UnityEngine.UIElements.UQueryState.ListQueryMatcher<object,object>
	// UnityEngine.UIElements.UQueryState<object>
	// }}

	public void RefMethods()
	{
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.EffectExecutor.<ExecuteStepsAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.EffectExecutor.<ExecuteStepsAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ResolveCardCastAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ResolveCardCastAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<ResolveSpellEffectsAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<ResolveSpellEffectsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameActions.<SettleResponseWindowAsync>d__19>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameActions.<SettleResponseWindowAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameLoopController.<ProcessPriority>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameLoopController.<ProcessPriority>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.GameLoopController.<ResolveStack>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.GameLoopController.<ResolveStack>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_1>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_1>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_3>d>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_3>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<BeginResolution>d__48>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<BeginResolution>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<FinishResolution>d__50>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<FinishResolution>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<PlayerPass>d__45>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<PlayerPass>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<ResolveStack>d__49>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<ResolveStack>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.StackEngine.<ResolveTop>d__55>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.StackEngine.<ResolveTop>d__55&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleController.<RunAiTurnAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleController.<RunAiTurnAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<DoCombatAsync>d__27>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<DoCombatAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.SimpleAI.<TakeTurnAsync>d__7>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.SimpleAI.<TakeTurnAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.UiTargetSelector.<RunCountdown>d__6>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.UiTargetSelector.<RunCountdown>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.CostCompensationService.<ExecutePayloadAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.CostCompensationService.<ExecutePayloadAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.DelayedEffectScheduler.<Fire>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.DelayedEffectScheduler.<Fire>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteFlatAsync>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteFlatAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.GameActions.<ResolveCardCastAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.GameActions.<ResolveCardCastAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.ProphecySystem.<ResolveAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.ProphecySystem.<ResolveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.LockRevealedAura.<PromptLockAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.LockRevealedAura.<PromptLockAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.SkipStandbyCountTracker.<PromptSkipStandbyAsync>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.SkipStandbyCountTracker.<PromptSkipStandbyAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.AbandonHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.AbandonHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.Handlers.ScryCardsHandler.<ExecuteAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.Handlers.ScryCardsHandler.<ExecuteAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.EffectExecutor.<ExecuteAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.EffectExecutor.<ExecuteAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameActions.<SettleResponseWindowAsync>d__19>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameActions.<SettleResponseWindowAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.GameCore.<EnforceHandLimitAsync>d__56>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.GameCore.<EnforceHandLimitAsync>d__56&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.HeroSkillSystem.<DiscoverFromDeckAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.HeroSkillSystem.<DiscoverFromDeckAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.HeroSkillSystem.<GraveyardCreatureToLandTappedAsync>d__18>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.HeroSkillSystem.<GraveyardCreatureToLandTappedAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.HeroSkillSystem.<RecycleFromGraveyardAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.HeroSkillSystem.<RecycleFromGraveyardAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.CostCompensationService.<PayWithCompensationAsync>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.CostCompensationService.<PayWithCompensationAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.HeroSkillSystem.<ActivateAsync>d__15>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.HeroSkillSystem.<ActivateAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,CardCore.GameActions.<ActivateHeroSkill>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,CardCore.GameActions.<ActivateHeroSkill>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter&,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>,CardCore.TargetSelectionService.<RequestAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<int,object,object>>&,CardCore.TargetSelectionService.<RequestAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<int>,CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<int>&,CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__10>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,CardCore.TargetSelectionService.<RequestAsync>d__13>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,CardCore.TargetSelectionService.<RequestAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.UiTargetSelector.<SelectIndicesAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.UiTargetSelector.<SelectIndicesAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.AbandonHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.AbandonHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.DeclareHandHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.DiscoverCardHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.LookAtTopCardsHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.RedirectTargetHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3>(CardCore.Attribute.Handlers.SacrificeHandler.<ExecuteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.Attribute.Handlers.ScryCardsHandler.<ExecuteAsync>d__4>(CardCore.Attribute.Handlers.ScryCardsHandler.<ExecuteAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.CostCompensationService.<ExecutePayloadAsync>d__3>(CardCore.CostCompensationService.<ExecutePayloadAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.DelayedEffectScheduler.<Fire>d__5>(CardCore.DelayedEffectScheduler.<Fire>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__16>(CardCore.EffectExecutor.<ApplyGateRewardsAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteAsync>d__8>(CardCore.EffectExecutor.<ExecuteAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteFlatAsync>d__9>(CardCore.EffectExecutor.<ExecuteFlatAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11>(CardCore.EffectExecutor.<ExecuteStepSequenceAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.EffectExecutor.<ExecuteStepsAsync>d__10>(CardCore.EffectExecutor.<ExecuteStepsAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveCardCastAsync>d__10>(CardCore.GameActions.<ResolveCardCastAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<ResolveSpellEffectsAsync>d__22>(CardCore.GameActions.<ResolveSpellEffectsAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameActions.<SettleResponseWindowAsync>d__19>(CardCore.GameActions.<SettleResponseWindowAsync>d__19&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameCore.<EnforceHandLimitAsync>d__56>(CardCore.GameCore.<EnforceHandLimitAsync>d__56&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameLoopController.<ProcessPriority>d__11>(CardCore.GameLoopController.<ProcessPriority>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.GameLoopController.<ResolveStack>d__12>(CardCore.GameLoopController.<ResolveStack>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_1>d>(CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_1>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_3>d>(CardCore.HeroSkillSystem.<>c.<<-cctor>b__24_3>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.HeroSkillSystem.<DiscoverFromDeckAsync>d__16>(CardCore.HeroSkillSystem.<DiscoverFromDeckAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.HeroSkillSystem.<GraveyardCreatureToLandTappedAsync>d__18>(CardCore.HeroSkillSystem.<GraveyardCreatureToLandTappedAsync>d__18&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.HeroSkillSystem.<RecycleFromGraveyardAsync>d__22>(CardCore.HeroSkillSystem.<RecycleFromGraveyardAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.LockRevealedAura.<PromptLockAsync>d__12>(CardCore.LockRevealedAura.<PromptLockAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.ProphecySystem.<ResolveAsync>d__10>(CardCore.ProphecySystem.<ResolveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.SkipStandbyCountTracker.<PromptSkipStandbyAsync>d__9>(CardCore.SkipStandbyCountTracker.<PromptSkipStandbyAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<BeginResolution>d__48>(CardCore.StackEngine.<BeginResolution>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<FinishResolution>d__50>(CardCore.StackEngine.<FinishResolution>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<PlayerPass>d__45>(CardCore.StackEngine.<PlayerPass>d__45&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<ResolveStack>d__49>(CardCore.StackEngine.<ResolveStack>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<CardCore.StackEngine.<ResolveTop>d__55>(CardCore.StackEngine.<ResolveTop>d__55&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.BattleController.<RunAiTurnAsync>d__20>(SynergyUI.BattleController.<RunAiTurnAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<DoCombatAsync>d__27>(SynergyUI.SimpleAI.<DoCombatAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.SimpleAI.<TakeTurnAsync>d__7>(SynergyUI.SimpleAI.<TakeTurnAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<SynergyUI.UiTargetSelector.<RunCountdown>d__6>(SynergyUI.UiTargetSelector.<RunCountdown>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5>(CardCore.Attribute.EffectHandlerRegistry.<ExecuteEffectAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.CostCompensationService.<PayWithCompensationAsync>d__0>(CardCore.CostCompensationService.<PayWithCompensationAsync>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.GameActions.<ActivateHeroSkill>d__2>(CardCore.GameActions.<ActivateHeroSkill>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<CardCore.HeroSkillSystem.<ActivateAsync>d__15>(CardCore.HeroSkillSystem.<ActivateAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13>(CardCore.Network.NetworkTargetSelector.<SelectOneAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Start<CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14>(CardCore.TargetSelectionService.<RequestOneIndexAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8>(CardCore.Attribute.EffectHandlerRegistry.<ResolveCompositionTargetsAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__10>(CardCore.Attribute.EffectHandlerRegistry.<ResolveTargetsInteractiveAsync>d__10&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2>(CardCore.Attribute.Handlers.DeckArrangeHelper.<ArrangeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12>(CardCore.Network.NetworkTargetSelector.<SelectAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<CardCore.TargetSelectionService.<RequestAsync>d__13>(CardCore.TargetSelectionService.<RequestAsync>d__13&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cysharp.Threading.Tasks.UniTaskExtensions.<ContinueWith>d__60<object>>(Cysharp.Threading.Tasks.UniTaskExtensions.<ContinueWith>d__60<object>&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<SynergyUI.UiTargetSelector.<SelectIndicesAsync>d__3>(SynergyUI.UiTargetSelector.<SelectIndicesAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,SynergyUI.BattleScreen.<ActivateSkillAsync>d__67>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,SynergyUI.BattleScreen.<ActivateSkillAsync>d__67&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,SynergyUI.BattleScreen.<DrainNetSelects>d__35>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,SynergyUI.BattleScreen.<DrainNetSelects>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,SynergyUI.BattleScreen.<StartNetAsync>d__27>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,SynergyUI.BattleScreen.<StartNetAsync>d__27&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<ActivateSkillAsync>d__67>(SynergyUI.BattleScreen.<ActivateSkillAsync>d__67&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<DrainNetSelects>d__35>(SynergyUI.BattleScreen.<DrainNetSelects>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<SynergyUI.BattleScreen.<StartNetAsync>d__27>(SynergyUI.BattleScreen.<StartNetAsync>d__27&)
		// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>> Cysharp.Threading.Tasks.Internal.StateTuple.Create<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>)
		// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<int,object,object>> Cysharp.Threading.Tasks.UniTask.WhenAny<object,object>(Cysharp.Threading.Tasks.UniTask<object>,Cysharp.Threading.Tasks.UniTask<object>)
		// Cysharp.Threading.Tasks.UniTask<object> Cysharp.Threading.Tasks.UniTaskExtensions.ContinueWith<object>(Cysharp.Threading.Tasks.UniTask,System.Func<object>)
		// System.Void Cysharp.Threading.Tasks.UniTaskExtensions.Forget<byte>(Cysharp.Threading.Tasks.UniTask<byte>)
		// System.Void MemoryPack.IMemoryPackFormatter<object>.Serialize<object>(MemoryPack.MemoryPackWriter<object>&,object&)
		// byte[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<byte>(int,bool)
		// int[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<int>(int,bool)
		// object[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<object>(int,bool)
		// uint[] MemoryPack.Internal.MemoryMarshalEx.AllocateUninitializedArray<uint>(int,bool)
		// byte& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<byte>(byte[])
		// int& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<int>(int[])
		// object& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<object>(object[])
		// uint& MemoryPack.Internal.MemoryMarshalEx.GetArrayDataReference<uint>(uint[])
		// MemoryPack.MemoryPackFormatter<object> MemoryPack.MemoryPackFormatterProvider.GetFormatter<object>()
		// bool MemoryPack.MemoryPackFormatterProvider.IsRegistered<System.Nullable<int>>()
		// bool MemoryPack.MemoryPackFormatterProvider.IsRegistered<int>()
		// bool MemoryPack.MemoryPackFormatterProvider.IsRegistered<object>()
		// System.Void MemoryPack.MemoryPackFormatterProvider.Register<System.Nullable<int>>(MemoryPack.MemoryPackFormatter<System.Nullable<int>>)
		// System.Void MemoryPack.MemoryPackFormatterProvider.Register<int>(MemoryPack.MemoryPackFormatter<int>)
		// System.Void MemoryPack.MemoryPackFormatterProvider.Register<object>(MemoryPack.MemoryPackFormatter<object>)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanaged<System.Nullable<int>>(System.Nullable<int>&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanaged<int,System.Nullable<int>,System.Nullable<int>>(int&,System.Nullable<int>&,System.Nullable<int>&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<byte>(byte[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<int>(int[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<object>(object[]&)
		// System.Void MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<uint>(uint[]&)
		// byte[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<byte>()
		// int[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<int>()
		// uint[] MemoryPack.MemoryPackReader.DangerousReadUnmanagedArray<uint>()
		// MemoryPack.IMemoryPackFormatter<object> MemoryPack.MemoryPackReader.GetFormatter<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadArray<object>(object[]&)
		// object[] MemoryPack.MemoryPackReader.ReadArray<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadPackable<object>(object&)
		// object MemoryPack.MemoryPackReader.ReadPackable<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadPackableArray<object>(object[]&)
		// object[] MemoryPack.MemoryPackReader.ReadPackableArray<object>()
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte,int,int>(byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<byte>(byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<double>(double&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float,int,int,int>(float&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float,int>(float&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<float>(float&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,byte,uint>(int&,byte&,uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,byte>(int&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,float>(int&,float&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,byte,byte>(int&,int&,byte&,byte&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,byte,int,int,byte,uint,int>(int&,int&,byte&,int&,int&,byte&,uint&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,byte,int,int,int,int,int>(int&,int&,byte&,int&,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int,int,int>(int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,int>(int&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,uint,ushort,long>(int&,uint&,ushort&,long&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int,uint>(int&,uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<int>(int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<long>(long&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<uint,int>(uint&,int&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<uint>(uint&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanaged<ushort>(ushort&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<byte>(byte[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<int>(int[]&)
		// System.Void MemoryPack.MemoryPackReader.ReadUnmanagedArray<uint>(uint[]&)
		// byte[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<byte>()
		// int[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<int>()
		// uint[] MemoryPack.MemoryPackReader.ReadUnmanagedArray<uint>()
		// System.Void MemoryPack.MemoryPackReader.ReadValue<object>(object&)
		// object MemoryPack.MemoryPackReader.ReadValue<object>()
		// int MemoryPack.MemoryPackSerializer.Deserialize<object>(System.ReadOnlySpan<byte>,object&,MemoryPack.MemoryPackSerializerOptions)
		// object MemoryPack.MemoryPackSerializer.Deserialize<object>(System.ReadOnlySpan<byte>,MemoryPack.MemoryPackSerializerOptions)
		// System.Void MemoryPack.MemoryPackSerializer.Serialize<object,object>(MemoryPack.MemoryPackWriter<object>&,object&)
		// byte[] MemoryPack.MemoryPackSerializer.Serialize<object>(object&,MemoryPack.MemoryPackSerializerOptions)
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanaged<int,System.Nullable<int>,System.Nullable<int>>(int&,System.Nullable<int>&,System.Nullable<int>&)
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<byte>(byte[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<int>(int[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.DangerousWriteUnmanagedArray<uint>(uint[])
		// MemoryPack.IMemoryPackFormatter<object> MemoryPack.MemoryPackWriter<object>.GetFormatter<object>()
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WritePackable<object>(object&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WritePackableArray<object>(object[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte,int,int>(byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<byte>(byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<double>(double&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<float,int,int,int>(float&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<float,int>(float&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,byte,uint>(int&,byte&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,byte>(int&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int,byte,byte>(int&,int&,byte&,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int,byte,int,int,byte,uint,int>(int&,int&,byte&,int&,int&,byte&,uint&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int,byte,int,int,int,int,int>(int&,int&,byte&,int&,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,int>(int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int,uint>(int&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<int>(int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<long>(long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanaged<uint>(uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<byte>(byte[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<int>(int[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedArray<uint>(uint[])
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<byte,int,int>(byte,byte&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<byte>(byte,byte&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<float,int>(byte,float&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,float>(byte,int&,float&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int,int,int>(byte,int&,int&,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,int>(byte,int&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,uint,ushort,long>(byte,int&,uint&,ushort&,long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int,uint>(byte,int&,uint&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<int>(byte,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<long>(byte,long&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteUnmanagedWithObjectHeader<uint,int>(byte,uint&,int&)
		// System.Void MemoryPack.MemoryPackWriter<object>.WriteValue<object>(object&)
		// object System.Activator.CreateInstance<object>()
		// System.ValueTuple<int,object>[] System.Array.Empty<System.ValueTuple<int,object>>()
		// byte[] System.Array.Empty<byte>()
		// float[] System.Array.Empty<float>()
		// int[] System.Array.Empty<int>()
		// object[] System.Array.Empty<object>()
		// uint[] System.Array.Empty<uint>()
		// int System.Array.FindIndex<System.ValueTuple<object,int>>(System.ValueTuple<object,int>[],System.Predicate<System.ValueTuple<object,int>>)
		// int System.Array.FindIndex<System.ValueTuple<object,int>>(System.ValueTuple<object,int>[],int,int,System.Predicate<System.ValueTuple<object,int>>)
		// int System.Array.IndexOf<int>(int[],int)
		// int System.Array.IndexOf<object>(object[],object)
		// int System.Array.IndexOfImpl<int>(int[],int,int,int)
		// int System.Array.IndexOfImpl<object>(object[],object,int,int)
		// float System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,float>(System.Collections.Generic.IReadOnlyDictionary<int,float>,int)
		// float System.Collections.Generic.CollectionExtensions.GetValueOrDefault<int,float>(System.Collections.Generic.IReadOnlyDictionary<int,float>,int,float)
		// int System.Collections.Generic.CollectionExtensions.GetValueOrDefault<object,int>(System.Collections.Generic.IReadOnlyDictionary<object,int>,object,int)
		// bool System.Enum.TryParse<int>(string,bool,int&)
		// bool System.Enum.TryParse<int>(string,int&)
		// bool System.Enum.TryParse<object>(string,bool,object&)
		// bool System.Enum.TryParse<object>(string,object&)
		// int System.HashCode.Combine<int,System.Nullable<int>>(int,System.Nullable<int>)
		// int System.HashCode.Combine<uint,System.DateTime>(uint,System.DateTime)
		// bool System.Linq.Enumerable.All<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Any<int>(System.Collections.Generic.IEnumerable<int>)
		// bool System.Linq.Enumerable.Any<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Cast<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.CastIterator<object>(System.Collections.IEnumerable)
		// bool System.Linq.Enumerable.Contains<int>(System.Collections.Generic.IEnumerable<int>,int)
		// bool System.Linq.Enumerable.Contains<int>(System.Collections.Generic.IEnumerable<int>,int,System.Collections.Generic.IEqualityComparer<int>)
		// bool System.Linq.Enumerable.Contains<object>(System.Collections.Generic.IEnumerable<object>,object)
		// bool System.Linq.Enumerable.Contains<object>(System.Collections.Generic.IEnumerable<object>,object,System.Collections.Generic.IEqualityComparer<object>)
		// int System.Linq.Enumerable.Count<System.Collections.Generic.KeyValuePair<int,object>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>)
		// int System.Linq.Enumerable.Count<object>(System.Collections.Generic.IEnumerable<object>)
		// int System.Linq.Enumerable.Count<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Distinct<int>(System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Distinct<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.DistinctIterator<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.DistinctIterator<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Empty<object>()
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Except<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.ExceptIterator<int>(System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEnumerable<int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.KeyValuePair<int,float> System.Linq.Enumerable.First<System.Collections.Generic.KeyValuePair<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>)
		// object System.Linq.Enumerable.First<object>(System.Collections.Generic.IEnumerable<object>)
		// object System.Linq.Enumerable.FirstOrDefault<object>(System.Collections.Generic.IEnumerable<object>)
		// object System.Linq.Enumerable.FirstOrDefault<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<System.Linq.IGrouping<int,System.Collections.Generic.KeyValuePair<int,object>>> System.Linq.Enumerable.GroupBy<System.Collections.Generic.KeyValuePair<int,object>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>,System.Func<System.Collections.Generic.KeyValuePair<int,object>,int>)
		// System.Collections.Generic.IEnumerable<System.Linq.IGrouping<object,object>> System.Linq.Enumerable.GroupBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// object System.Linq.Enumerable.Last<object>(System.Collections.Generic.IEnumerable<object>)
		// object System.Linq.Enumerable.LastOrDefault<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.OfType<object>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.OfTypeIterator<object>(System.Collections.IEnumerable)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.OrderBy<System.Collections.Generic.KeyValuePair<int,float>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>)
		// System.Linq.IOrderedEnumerable<int> System.Linq.Enumerable.OrderBy<int,int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,System.Guid>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Guid>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Collections.Generic.IComparer<object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.OrderByDescending<System.Collections.Generic.KeyValuePair<int,float>,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.OrderByDescending<System.ValueTuple<int,float,float>,float>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,byte>(System.Collections.Generic.IEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Reverse<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.ReverseIterator<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Select<float,float>(System.Collections.Generic.IEnumerable<float>,System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,int>,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Select<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,float>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<int,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<object,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<int,object>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<int,object>>,System.Func<System.ValueTuple<int,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,int>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>,System.Func<System.ValueTuple<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.ValueTuple<object,object>,object>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>,System.Func<System.ValueTuple<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<int,object>(System.Collections.Generic.IEnumerable<int>,System.Func<int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Select<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectMany<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Collections.Generic.IEnumerable<object>>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectManyIterator<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,System.Collections.Generic.IEnumerable<object>>)
		// bool System.Linq.Enumerable.SequenceEqual<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>)
		// bool System.Linq.Enumerable.SequenceEqual<object>(System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEnumerable<object>,System.Collections.Generic.IEqualityComparer<object>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Skip<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.SkipIterator<int>(System.Collections.Generic.IEnumerable<int>,int)
		// float System.Linq.Enumerable.Sum<float>(System.Collections.Generic.IEnumerable<float>,System.Func<float,float>)
		// int System.Linq.Enumerable.Sum<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Take<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Take<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.TakeIterator<int>(System.Collections.Generic.IEnumerable<int>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.TakeIterator<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.TakeLast<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.TakeLastIterator<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.ThenBy<System.ValueTuple<int,float,float>,int>(System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,System.DateTime>(System.Linq.IOrderedEnumerable<object>,System.Func<object,System.DateTime>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,byte>(System.Linq.IOrderedEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,float>(System.Linq.IOrderedEnumerable<object>,System.Func<object,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,int>(System.Linq.IOrderedEnumerable<object>,System.Func<object,int>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,object>(System.Linq.IOrderedEnumerable<object>,System.Func<object,object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,object>(System.Linq.IOrderedEnumerable<object>,System.Func<object,object>,System.Collections.Generic.IComparer<object>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenBy<object,uint>(System.Linq.IOrderedEnumerable<object>,System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.Enumerable.ThenByDescending<System.ValueTuple<int,float,float>,float>(System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>,System.Func<System.ValueTuple<int,float,float>,float>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenByDescending<object,System.DateTime>(System.Linq.IOrderedEnumerable<object>,System.Func<object,System.DateTime>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenByDescending<object,byte>(System.Linq.IOrderedEnumerable<object>,System.Func<object,byte>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.ThenByDescending<object,int>(System.Linq.IOrderedEnumerable<object>,System.Func<object,int>)
		// int[] System.Linq.Enumerable.ToArray<int>(System.Collections.Generic.IEnumerable<int>)
		// object[] System.Linq.Enumerable.ToArray<object>(System.Collections.Generic.IEnumerable<object>)
		// uint[] System.Linq.Enumerable.ToArray<uint>(System.Collections.Generic.IEnumerable<uint>)
		// System.Collections.Generic.Dictionary<int,float> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,float>,int,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>)
		// System.Collections.Generic.Dictionary<int,float> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,float>,int,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,float>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<int,float> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,float>)
		// System.Collections.Generic.Dictionary<int,float> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,float>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,float>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<int,int> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.Dictionary<int,int> System.Linq.Enumerable.ToDictionary<System.Collections.Generic.KeyValuePair<int,int>,int,int>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<int,int> System.Linq.Enumerable.ToDictionary<object,int,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>,System.Func<object,int>)
		// System.Collections.Generic.Dictionary<int,int> System.Linq.Enumerable.ToDictionary<object,int,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>,System.Func<object,int>,System.Collections.Generic.IEqualityComparer<int>)
		// System.Collections.Generic.Dictionary<uint,object> System.Linq.Enumerable.ToDictionary<object,uint,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>,System.Func<object,object>,System.Collections.Generic.IEqualityComparer<uint>)
		// System.Collections.Generic.Dictionary<uint,object> System.Linq.Enumerable.ToDictionary<object,uint>(System.Collections.Generic.IEnumerable<object>,System.Func<object,uint>)
		// System.Collections.Generic.List<System.ValueTuple<object,int>> System.Linq.Enumerable.ToList<System.ValueTuple<object,int>>(System.Collections.Generic.IEnumerable<System.ValueTuple<object,int>>)
		// System.Collections.Generic.List<int> System.Linq.Enumerable.ToList<int>(System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.List<object> System.Linq.Enumerable.ToList<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,float>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,float>>,System.Func<System.Collections.Generic.KeyValuePair<int,float>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<int,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>,System.Func<System.Collections.Generic.KeyValuePair<int,int>,bool>)
		// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>> System.Linq.Enumerable.Where<System.Collections.Generic.KeyValuePair<object,int>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,bool>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Where<int>(System.Collections.Generic.IEnumerable<int>,System.Func<int,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Iterator<float>.Select<float>(System.Func<float,float>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>.Select<int>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,int>)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Iterator<object>.Select<int>(System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,float>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<int,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<int,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<int,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<int,object>>.Select<object>(System.Func<System.ValueTuple<int,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,int>>.Select<object>(System.Func<System.ValueTuple<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.ValueTuple<object,object>>.Select<object>(System.Func<System.ValueTuple<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<int>.Select<object>(System.Func<int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<object>.Select<object>(System.Func<object,object>)
		// System.Collections.Generic.IEnumerable<uint> System.Linq.Enumerable.Iterator<object>.Select<uint>(System.Func<object,uint>)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>.CreateOrderedEnumerable<float>(System.Func<System.ValueTuple<int,float,float>,float>,System.Collections.Generic.IComparer<float>,bool)
		// System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>> System.Linq.IOrderedEnumerable<System.ValueTuple<int,float,float>>.CreateOrderedEnumerable<int>(System.Func<System.ValueTuple<int,float,float>,int>,System.Collections.Generic.IComparer<int>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<System.DateTime>(System.Func<object,System.DateTime>,System.Collections.Generic.IComparer<System.DateTime>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<byte>(System.Func<object,byte>,System.Collections.Generic.IComparer<byte>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<float>(System.Func<object,float>,System.Collections.Generic.IComparer<float>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<int>(System.Func<object,int>,System.Collections.Generic.IComparer<int>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<object>(System.Func<object,object>,System.Collections.Generic.IComparer<object>,bool)
		// System.Linq.IOrderedEnumerable<object> System.Linq.IOrderedEnumerable<object>.CreateOrderedEnumerable<uint>(System.Func<object,uint>,System.Collections.Generic.IComparer<uint>,bool)
		// System.Span<byte> System.MemoryExtensions.AsSpan<byte>(byte[])
		// System.Span<int> System.MemoryExtensions.AsSpan<int>(int[])
		// System.Span<object> System.MemoryExtensions.AsSpan<object>(object[])
		// System.Span<uint> System.MemoryExtensions.AsSpan<uint>(uint[])
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,SynergyUI.BattleScreen.<OnEndTurn>d__69>(Cysharp.Threading.Tasks.UniTask.Awaiter&,SynergyUI.BattleScreen.<OnEndTurn>d__69&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<SynergyUI.BattleScreen.<OnEndTurn>d__69>(SynergyUI.BattleScreen.<OnEndTurn>d__69&)
		// bool System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<object>()
		// byte& System.Runtime.CompilerServices.Unsafe.Add<byte>(byte&,int)
		// byte& System.Runtime.CompilerServices.Unsafe.As<byte,byte>(byte&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<int,byte>(int&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<object,byte>(object&)
		// byte& System.Runtime.CompilerServices.Unsafe.As<uint,byte>(uint&)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// object& System.Runtime.CompilerServices.Unsafe.AsRef<object>(object&)
		// System.Nullable<int> System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.Nullable<int>>(byte&)
		// byte System.Runtime.CompilerServices.Unsafe.ReadUnaligned<byte>(byte&)
		// double System.Runtime.CompilerServices.Unsafe.ReadUnaligned<double>(byte&)
		// float System.Runtime.CompilerServices.Unsafe.ReadUnaligned<float>(byte&)
		// int System.Runtime.CompilerServices.Unsafe.ReadUnaligned<int>(byte&)
		// long System.Runtime.CompilerServices.Unsafe.ReadUnaligned<long>(byte&)
		// object System.Runtime.CompilerServices.Unsafe.ReadUnaligned<object>(byte&)
		// uint System.Runtime.CompilerServices.Unsafe.ReadUnaligned<uint>(byte&)
		// ushort System.Runtime.CompilerServices.Unsafe.ReadUnaligned<ushort>(byte&)
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<System.Nullable<int>>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<byte>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<double>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<float>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<int>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<long>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<object>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<uint>()
		// int System.Runtime.CompilerServices.Unsafe.SizeOf<ushort>()
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<System.Nullable<int>>(byte&,System.Nullable<int>)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<byte>(byte&,byte)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<double>(byte&,double)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<float>(byte&,float)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<int>(byte&,int)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<long>(byte&,long)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<object>(byte&,object)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<uint>(byte&,uint)
		// System.Void System.Runtime.CompilerServices.Unsafe.WriteUnaligned<ushort>(byte&,ushort)
		// byte& System.Runtime.InteropServices.MemoryMarshal.GetReference<byte>(System.ReadOnlySpan<byte>)
		// byte& System.Runtime.InteropServices.MemoryMarshal.GetReference<byte>(System.Span<byte>)
		// int& System.Runtime.InteropServices.MemoryMarshal.GetReference<int>(System.Span<int>)
		// object& System.Runtime.InteropServices.MemoryMarshal.GetReference<object>(System.Span<object>)
		// uint& System.Runtime.InteropServices.MemoryMarshal.GetReference<uint>(System.Span<uint>)
		// System.Tuple<object,object> System.Tuple.Create<object,object>(object,object)
		// object& Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As<object,object>(object&)
		// System.Void Unity.Collections.LowLevel.Unsafe.UnsafeUtility.CopyPtrToStructure<HexMap.HexMapConfig>(System.Void*,HexMap.HexMapConfig&)
		// System.Void Unity.Collections.LowLevel.Unsafe.UnsafeUtility.InternalCopyPtrToStructure<HexMap.HexMapConfig>(System.Void*,HexMap.HexMapConfig&)
		// Unity.Entities.ComponentType Unity.Entities.ComponentType.ReadOnly<HexMap.TerrainPending>()
		// Unity.Entities.ComponentType Unity.Entities.ComponentType.ReadWrite<HexMap.TerrainPending>()
		// HexMap.HexMapConfig Unity.Entities.EntityDataAccess.GetComponentData<HexMap.HexMapConfig>(Unity.Entities.Entity)
		// HexMap.HexMapConfig Unity.Entities.EntityManager.GetComponentData<HexMap.HexMapConfig>(Unity.Entities.Entity)
		// Unity.Entities.TypeIndex Unity.Entities.TypeManager.GetTypeIndex<HexMap.HexMapConfig>()
		// Unity.Entities.TypeIndex Unity.Entities.TypeManager.GetTypeIndex<HexMap.TerrainPending>()
		// Unity.Entities.TypeIndex Unity.Entities.TypeManager.GetTypeIndex<object>()
		// System.Void Unity.Entities.TypeManager.ManagedException<HexMap.HexMapConfig>()
		// System.Void Unity.Entities.TypeManager.ManagedException<HexMap.TerrainPending>()
		// System.Void Unity.Entities.TypeManager.ManagedException<object>()
		// object Unity.Entities.World.GetExistingSystemManaged<object>()
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.JsonUtility.FromJson<object>(string)
		// object UnityEngine.Resources.Load<object>(string)
		// System.Void UnityEngine.UIElements.CallbackEventHandler.AddEventCategories<object>(UnityEngine.UIElements.CallbackOptions)
		// System.Void UnityEngine.UIElements.CallbackEventHandler.RegisterCallback<object>(UnityEngine.UIElements.EventCallback<object>,UnityEngine.UIElements.TrickleDown)
		// System.Void UnityEngine.UIElements.CallbackEventHandler.UnregisterCallback<object>(UnityEngine.UIElements.EventCallback<object>,UnityEngine.UIElements.TrickleDown)
		// System.Void UnityEngine.UIElements.EventCallbackRegistry.RegisterCallback<object>(UnityEngine.UIElements.EventCallback<object>,UnityEngine.UIElements.CallbackOptions)
		// bool UnityEngine.UIElements.EventCallbackRegistry.UnregisterCallback<object>(UnityEngine.UIElements.EventCallback<object>,UnityEngine.UIElements.CallbackOptions)
		// bool UnityEngine.UIElements.INotifyValueChangedExtensions.RegisterValueChangedCallback<byte>(UnityEngine.UIElements.INotifyValueChanged<byte>,UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ChangeEvent<byte>>)
		// bool UnityEngine.UIElements.INotifyValueChangedExtensions.RegisterValueChangedCallback<float>(UnityEngine.UIElements.INotifyValueChanged<float>,UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ChangeEvent<float>>)
		// bool UnityEngine.UIElements.INotifyValueChangedExtensions.RegisterValueChangedCallback<int>(UnityEngine.UIElements.INotifyValueChanged<int>,UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ChangeEvent<int>>)
		// bool UnityEngine.UIElements.INotifyValueChangedExtensions.RegisterValueChangedCallback<object>(UnityEngine.UIElements.INotifyValueChanged<object>,UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ChangeEvent<object>>)
		// object UnityEngine.UIElements.UQueryExtensions.Q<object>(UnityEngine.UIElements.VisualElement,string,string)
		// YooAsset.AssetHandle YooAsset.ResourcePackage.LoadAssetAsync<object>(string,uint)
		// string string.Join<int>(string,System.Collections.Generic.IEnumerable<int>)
		// string string.JoinCore<int>(System.Char*,int,System.Collections.Generic.IEnumerable<int>)
	}
}