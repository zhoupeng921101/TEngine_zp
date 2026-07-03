using System.Runtime.CompilerServices;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using System.Collections.Generic;
#pragma warning disable CS8618
namespace Fantasy
{
   public static class NetworkProtocolHelper
   {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ActivityIncrementResponse> C2G_ActivityIncrement(this Session session, C2G_ActivityIncrement C2G_ActivityIncrement_request)
		{
			return (G2C_ActivityIncrementResponse)await session.Call(C2G_ActivityIncrement_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ActivityIncrementResponse> C2G_ActivityIncrement(this Session session, int activityId, int delta)
		{
			using var C2G_ActivityIncrement_request = Fantasy.C2G_ActivityIncrement.Create();
			C2G_ActivityIncrement_request.ActivityId = activityId;
			C2G_ActivityIncrement_request.Delta = delta;
			return (G2C_ActivityIncrementResponse)await session.Call(C2G_ActivityIncrement_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_GameStartResponse> C2G_GameStartRequest(this Session session, C2G_GameStartRequest C2G_GameStartRequest_request)
		{
			return (G2C_GameStartResponse)await session.Call(C2G_GameStartRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_GameStartResponse> C2G_GameStartRequest(this Session session)
		{
			using var C2G_GameStartRequest_request = Fantasy.C2G_GameStartRequest.Create();
			return (G2C_GameStartResponse)await session.Call(C2G_GameStartRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PlaceResponse> C2G_PlaceRequest(this Session session, C2G_PlaceRequest C2G_PlaceRequest_request)
		{
			return (G2C_PlaceResponse)await session.Call(C2G_PlaceRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PlaceResponse> C2G_PlaceRequest(this Session session, long gameId, int baseStep, int candidateIndex, int posX, int posY, string sliceJson)
		{
			using var C2G_PlaceRequest_request = Fantasy.C2G_PlaceRequest.Create();
			C2G_PlaceRequest_request.GameId = gameId;
			C2G_PlaceRequest_request.BaseStep = baseStep;
			C2G_PlaceRequest_request.CandidateIndex = candidateIndex;
			C2G_PlaceRequest_request.PosX = posX;
			C2G_PlaceRequest_request.PosY = posY;
			C2G_PlaceRequest_request.SliceJson = sliceJson;
			return (G2C_PlaceResponse)await session.Call(C2G_PlaceRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ClearToolResponse> C2G_ClearToolRequest(this Session session, C2G_ClearToolRequest C2G_ClearToolRequest_request)
		{
			return (G2C_ClearToolResponse)await session.Call(C2G_ClearToolRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ClearToolResponse> C2G_ClearToolRequest(this Session session, long gameId, int baseStep, int posX, int posY, string sliceJson)
		{
			using var C2G_ClearToolRequest_request = Fantasy.C2G_ClearToolRequest.Create();
			C2G_ClearToolRequest_request.GameId = gameId;
			C2G_ClearToolRequest_request.BaseStep = baseStep;
			C2G_ClearToolRequest_request.PosX = posX;
			C2G_ClearToolRequest_request.PosY = posY;
			C2G_ClearToolRequest_request.SliceJson = sliceJson;
			return (G2C_ClearToolResponse)await session.Call(C2G_ClearToolRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_GameSnapshotResponse> C2G_GameSnapshotRequest(this Session session, C2G_GameSnapshotRequest C2G_GameSnapshotRequest_request)
		{
			return (G2C_GameSnapshotResponse)await session.Call(C2G_GameSnapshotRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_GameSnapshotResponse> C2G_GameSnapshotRequest(this Session session, long gameId)
		{
			using var C2G_GameSnapshotRequest_request = Fantasy.C2G_GameSnapshotRequest.Create();
			C2G_GameSnapshotRequest_request.GameId = gameId;
			return (G2C_GameSnapshotResponse)await session.Call(C2G_GameSnapshotRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_EquipCosmeticResponse> C2G_EquipCosmeticRequest(this Session session, C2G_EquipCosmeticRequest C2G_EquipCosmeticRequest_request)
		{
			return (G2C_EquipCosmeticResponse)await session.Call(C2G_EquipCosmeticRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_EquipCosmeticResponse> C2G_EquipCosmeticRequest(this Session session, int kind, int id)
		{
			using var C2G_EquipCosmeticRequest_request = Fantasy.C2G_EquipCosmeticRequest.Create();
			C2G_EquipCosmeticRequest_request.Kind = kind;
			C2G_EquipCosmeticRequest_request.Id = id;
			return (G2C_EquipCosmeticResponse)await session.Call(C2G_EquipCosmeticRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UnlockCosmeticResponse> C2G_UnlockCosmeticRequest(this Session session, C2G_UnlockCosmeticRequest C2G_UnlockCosmeticRequest_request)
		{
			return (G2C_UnlockCosmeticResponse)await session.Call(C2G_UnlockCosmeticRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UnlockCosmeticResponse> C2G_UnlockCosmeticRequest(this Session session, int kind, int id)
		{
			using var C2G_UnlockCosmeticRequest_request = Fantasy.C2G_UnlockCosmeticRequest.Create();
			C2G_UnlockCosmeticRequest_request.Kind = kind;
			C2G_UnlockCosmeticRequest_request.Id = id;
			return (G2C_UnlockCosmeticResponse)await session.Call(C2G_UnlockCosmeticRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UnlockCosmeticBatchResponse> C2G_UnlockCosmeticBatchRequest(this Session session, C2G_UnlockCosmeticBatchRequest C2G_UnlockCosmeticBatchRequest_request)
		{
			return (G2C_UnlockCosmeticBatchResponse)await session.Call(C2G_UnlockCosmeticBatchRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UnlockCosmeticBatchResponse> C2G_UnlockCosmeticBatchRequest(this Session session, List<UnlockCosmeticItem> items)
		{
			using var C2G_UnlockCosmeticBatchRequest_request = Fantasy.C2G_UnlockCosmeticBatchRequest.Create();
			C2G_UnlockCosmeticBatchRequest_request.Items = items;
			return (G2C_UnlockCosmeticBatchResponse)await session.Call(C2G_UnlockCosmeticBatchRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_EnterMainGameResponse> C2G_EnterMainGameRequest(this Session session, C2G_EnterMainGameRequest C2G_EnterMainGameRequest_request)
		{
			return (G2C_EnterMainGameResponse)await session.Call(C2G_EnterMainGameRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_EnterMainGameResponse> C2G_EnterMainGameRequest(this Session session)
		{
			using var C2G_EnterMainGameRequest_request = Fantasy.C2G_EnterMainGameRequest.Create();
			return (G2C_EnterMainGameResponse)await session.Call(C2G_EnterMainGameRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_LoginGameResponse> C2G_LoginGameRequest(this Session session, C2G_LoginGameRequest C2G_LoginGameRequest_request)
		{
			return (G2C_LoginGameResponse)await session.Call(C2G_LoginGameRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_LoginGameResponse> C2G_LoginGameRequest(this Session session, string accountName, string localPlayerId)
		{
			using var C2G_LoginGameRequest_request = Fantasy.C2G_LoginGameRequest.Create();
			C2G_LoginGameRequest_request.AccountName = accountName;
			C2G_LoginGameRequest_request.LocalPlayerId = localPlayerId;
			return (G2C_LoginGameResponse)await session.Call(C2G_LoginGameRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_GoddessClaimResponse> C2G_GoddessClaimRequest(this Session session, C2G_GoddessClaimRequest C2G_GoddessClaimRequest_request)
		{
			return (G2C_GoddessClaimResponse)await session.Call(C2G_GoddessClaimRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_GoddessClaimResponse> C2G_GoddessClaimRequest(this Session session, int reserved)
		{
			using var C2G_GoddessClaimRequest_request = Fantasy.C2G_GoddessClaimRequest.Create();
			C2G_GoddessClaimRequest_request.Reserved = reserved;
			return (G2C_GoddessClaimResponse)await session.Call(C2G_GoddessClaimRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UseItemResponse> C2G_UseItem(this Session session, C2G_UseItem C2G_UseItem_request)
		{
			return (G2C_UseItemResponse)await session.Call(C2G_UseItem_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UseItemResponse> C2G_UseItem(this Session session, int itemId, long count, long reqSeq)
		{
			using var C2G_UseItem_request = Fantasy.C2G_UseItem.Create();
			C2G_UseItem_request.ItemId = itemId;
			C2G_UseItem_request.Count = count;
			C2G_UseItem_request.ReqSeq = reqSeq;
			return (G2C_UseItemResponse)await session.Call(C2G_UseItem_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_InventoryDeltaPush(this Session session, G2C_InventoryDeltaPush G2C_InventoryDeltaPush_message)
		{
			session.Send(G2C_InventoryDeltaPush_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_InventoryDeltaPush(this Session session, List<InventoryHolding> holdings, List<InventoryLot> lots, long serverNowMs, bool loaded)
		{
			using var G2C_InventoryDeltaPush_message = Fantasy.G2C_InventoryDeltaPush.Create();
			G2C_InventoryDeltaPush_message.Holdings = holdings;
			G2C_InventoryDeltaPush_message.Lots = lots;
			G2C_InventoryDeltaPush_message.ServerNowMs = serverNowMs;
			G2C_InventoryDeltaPush_message.Loaded = loaded;
			session.Send(G2C_InventoryDeltaPush_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MailListResponse> C2G_MailListRequest(this Session session, C2G_MailListRequest C2G_MailListRequest_request)
		{
			return (G2C_MailListResponse)await session.Call(C2G_MailListRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MailListResponse> C2G_MailListRequest(this Session session)
		{
			using var C2G_MailListRequest_request = Fantasy.C2G_MailListRequest.Create();
			return (G2C_MailListResponse)await session.Call(C2G_MailListRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MailClaimResponse> C2G_MailClaimRequest(this Session session, C2G_MailClaimRequest C2G_MailClaimRequest_request)
		{
			return (G2C_MailClaimResponse)await session.Call(C2G_MailClaimRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MailClaimResponse> C2G_MailClaimRequest(this Session session, string mailId)
		{
			using var C2G_MailClaimRequest_request = Fantasy.C2G_MailClaimRequest.Create();
			C2G_MailClaimRequest_request.MailId = mailId;
			return (G2C_MailClaimResponse)await session.Call(C2G_MailClaimRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DeliverOrderResponse> C2G_DeliverOrderRequest(this Session session, C2G_DeliverOrderRequest C2G_DeliverOrderRequest_request)
		{
			return (G2C_DeliverOrderResponse)await session.Call(C2G_DeliverOrderRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DeliverOrderResponse> C2G_DeliverOrderRequest(this Session session, int slot)
		{
			using var C2G_DeliverOrderRequest_request = Fantasy.C2G_DeliverOrderRequest.Create();
			C2G_DeliverOrderRequest_request.Slot = slot;
			return (G2C_DeliverOrderResponse)await session.Call(C2G_DeliverOrderRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestEmptyMessage(this Session session, C2G_TestEmptyMessage C2G_TestEmptyMessage_message)
		{
			session.Send(C2G_TestEmptyMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestEmptyMessage(this Session session)
		{
			using var message = Fantasy.C2G_TestEmptyMessage.Create();
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestMessage(this Session session, C2G_TestMessage C2G_TestMessage_message)
		{
			session.Send(C2G_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestMessage(this Session session, string tag)
		{
			using var C2G_TestMessage_message = Fantasy.C2G_TestMessage.Create();
			C2G_TestMessage_message.Tag = tag;
			session.Send(C2G_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestResponse> C2G_TestRequest(this Session session, C2G_TestRequest C2G_TestRequest_request)
		{
			return (G2C_TestResponse)await session.Call(C2G_TestRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestResponse> C2G_TestRequest(this Session session, string tag, List<byte> data)
		{
			using var C2G_TestRequest_request = Fantasy.C2G_TestRequest.Create();
			C2G_TestRequest_request.Tag = tag;
			C2G_TestRequest_request.Data = data;
			return (G2C_TestResponse)await session.Call(C2G_TestRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestRequestPushMessage(this Session session, C2G_TestRequestPushMessage C2G_TestRequestPushMessage_message)
		{
			session.Send(C2G_TestRequestPushMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestRequestPushMessage(this Session session)
		{
			using var message = Fantasy.C2G_TestRequestPushMessage.Create();
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_PushMessage(this Session session, G2C_PushMessage G2C_PushMessage_message)
		{
			session.Send(G2C_PushMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_PushMessage(this Session session, string tag)
		{
			using var G2C_PushMessage_message = Fantasy.G2C_PushMessage.Create();
			G2C_PushMessage_message.Tag = tag;
			session.Send(G2C_PushMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateAddressableResponse> C2G_CreateAddressableRequest(this Session session, C2G_CreateAddressableRequest C2G_CreateAddressableRequest_request)
		{
			return (G2C_CreateAddressableResponse)await session.Call(C2G_CreateAddressableRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateAddressableResponse> C2G_CreateAddressableRequest(this Session session)
		{
			using var C2G_CreateAddressableRequest_request = Fantasy.C2G_CreateAddressableRequest.Create();
			return (G2C_CreateAddressableResponse)await session.Call(C2G_CreateAddressableRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2M_TestMessage(this Session session, C2M_TestMessage C2M_TestMessage_message)
		{
			session.Send(C2M_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2M_TestMessage(this Session session, string tag)
		{
			using var C2M_TestMessage_message = Fantasy.C2M_TestMessage.Create();
			C2M_TestMessage_message.Tag = tag;
			session.Send(C2M_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<M2C_TestResponse> C2M_TestRequest(this Session session, C2M_TestRequest C2M_TestRequest_request)
		{
			return (M2C_TestResponse)await session.Call(C2M_TestRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<M2C_TestResponse> C2M_TestRequest(this Session session, string tag)
		{
			using var C2M_TestRequest_request = Fantasy.C2M_TestRequest.Create();
			C2M_TestRequest_request.Tag = tag;
			return (M2C_TestResponse)await session.Call(C2M_TestRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateChatRouteResponse> C2G_CreateChatRouteRequest(this Session session, C2G_CreateChatRouteRequest C2G_CreateChatRouteRequest_request)
		{
			return (G2C_CreateChatRouteResponse)await session.Call(C2G_CreateChatRouteRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateChatRouteResponse> C2G_CreateChatRouteRequest(this Session session)
		{
			using var C2G_CreateChatRouteRequest_request = Fantasy.C2G_CreateChatRouteRequest.Create();
			return (G2C_CreateChatRouteResponse)await session.Call(C2G_CreateChatRouteRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestMessage(this Session session, C2Chat_TestMessage C2Chat_TestMessage_message)
		{
			session.Send(C2Chat_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestMessage(this Session session, string tag)
		{
			using var C2Chat_TestMessage_message = Fantasy.C2Chat_TestMessage.Create();
			C2Chat_TestMessage_message.Tag = tag;
			session.Send(C2Chat_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<Chat2C_TestMessageResponse> C2Chat_TestMessageRequest(this Session session, C2Chat_TestMessageRequest C2Chat_TestMessageRequest_request)
		{
			return (Chat2C_TestMessageResponse)await session.Call(C2Chat_TestMessageRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<Chat2C_TestMessageResponse> C2Chat_TestMessageRequest(this Session session, string tag)
		{
			using var C2Chat_TestMessageRequest_request = Fantasy.C2Chat_TestMessageRequest.Create();
			C2Chat_TestMessageRequest_request.Tag = tag;
			return (Chat2C_TestMessageResponse)await session.Call(C2Chat_TestMessageRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<M2C_MoveToMapResponse> C2M_MoveToMapRequest(this Session session, C2M_MoveToMapRequest C2M_MoveToMapRequest_request)
		{
			return (M2C_MoveToMapResponse)await session.Call(C2M_MoveToMapRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<M2C_MoveToMapResponse> C2M_MoveToMapRequest(this Session session)
		{
			using var C2M_MoveToMapRequest_request = Fantasy.C2M_MoveToMapRequest.Create();
			return (M2C_MoveToMapResponse)await session.Call(C2M_MoveToMapRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_SendAddressableToMap(this Session session, C2G_SendAddressableToMap C2G_SendAddressableToMap_message)
		{
			session.Send(C2G_SendAddressableToMap_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_SendAddressableToMap(this Session session, string tag)
		{
			using var C2G_SendAddressableToMap_message = Fantasy.C2G_SendAddressableToMap.Create();
			C2G_SendAddressableToMap_message.Tag = tag;
			session.Send(C2G_SendAddressableToMap_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestRequestPushMessage(this Session session, C2Chat_TestRequestPushMessage C2Chat_TestRequestPushMessage_message)
		{
			session.Send(C2Chat_TestRequestPushMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestRequestPushMessage(this Session session)
		{
			using var message = Fantasy.C2Chat_TestRequestPushMessage.Create();
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Chat2C_PushMessage(this Session session, Chat2C_PushMessage Chat2C_PushMessage_message)
		{
			session.Send(Chat2C_PushMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Chat2C_PushMessage(this Session session, string tag)
		{
			using var Chat2C_PushMessage_message = Fantasy.Chat2C_PushMessage.Create();
			Chat2C_PushMessage_message.Tag = tag;
			session.Send(Chat2C_PushMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateSubSceneResponse> C2G_CreateSubSceneRequest(this Session session, C2G_CreateSubSceneRequest C2G_CreateSubSceneRequest_request)
		{
			return (G2C_CreateSubSceneResponse)await session.Call(C2G_CreateSubSceneRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateSubSceneResponse> C2G_CreateSubSceneRequest(this Session session)
		{
			using var C2G_CreateSubSceneRequest_request = Fantasy.C2G_CreateSubSceneRequest.Create();
			return (G2C_CreateSubSceneResponse)await session.Call(C2G_CreateSubSceneRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_SendToSubSceneMessage(this Session session, C2G_SendToSubSceneMessage C2G_SendToSubSceneMessage_message)
		{
			session.Send(C2G_SendToSubSceneMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_SendToSubSceneMessage(this Session session)
		{
			using var message = Fantasy.C2G_SendToSubSceneMessage.Create();
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateSubSceneAddressableResponse> C2G_CreateSubSceneAddressableRequest(this Session session, C2G_CreateSubSceneAddressableRequest C2G_CreateSubSceneAddressableRequest_request)
		{
			return (G2C_CreateSubSceneAddressableResponse)await session.Call(C2G_CreateSubSceneAddressableRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_CreateSubSceneAddressableResponse> C2G_CreateSubSceneAddressableRequest(this Session session)
		{
			using var C2G_CreateSubSceneAddressableRequest_request = Fantasy.C2G_CreateSubSceneAddressableRequest.Create();
			return (G2C_CreateSubSceneAddressableResponse)await session.Call(C2G_CreateSubSceneAddressableRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2SubScene_TestMessage(this Session session, C2SubScene_TestMessage C2SubScene_TestMessage_message)
		{
			session.Send(C2SubScene_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2SubScene_TestMessage(this Session session, string tag)
		{
			using var C2SubScene_TestMessage_message = Fantasy.C2SubScene_TestMessage.Create();
			C2SubScene_TestMessage_message.Tag = tag;
			session.Send(C2SubScene_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2SubScene_TestDisposeMessage(this Session session, C2SubScene_TestDisposeMessage C2SubScene_TestDisposeMessage_message)
		{
			session.Send(C2SubScene_TestDisposeMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2SubScene_TestDisposeMessage(this Session session)
		{
			using var message = Fantasy.C2SubScene_TestDisposeMessage.Create();
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ConnectRoamingResponse> C2G_ConnectRoamingRequest(this Session session, C2G_ConnectRoamingRequest C2G_ConnectRoamingRequest_request)
		{
			return (G2C_ConnectRoamingResponse)await session.Call(C2G_ConnectRoamingRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ConnectRoamingResponse> C2G_ConnectRoamingRequest(this Session session)
		{
			using var C2G_ConnectRoamingRequest_request = Fantasy.C2G_ConnectRoamingRequest.Create();
			return (G2C_ConnectRoamingResponse)await session.Call(C2G_ConnectRoamingRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestRoamingMessage(this Session session, C2Chat_TestRoamingMessage C2Chat_TestRoamingMessage_message)
		{
			session.Send(C2Chat_TestRoamingMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestRoamingMessage(this Session session, string tag)
		{
			using var C2Chat_TestRoamingMessage_message = Fantasy.C2Chat_TestRoamingMessage.Create();
			C2Chat_TestRoamingMessage_message.Tag = tag;
			session.Send(C2Chat_TestRoamingMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Map_TestRoamingMessage(this Session session, C2Map_TestRoamingMessage C2Map_TestRoamingMessage_message)
		{
			session.Send(C2Map_TestRoamingMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Map_TestRoamingMessage(this Session session, string tag)
		{
			using var C2Map_TestRoamingMessage_message = Fantasy.C2Map_TestRoamingMessage.Create();
			C2Map_TestRoamingMessage_message.Tag = tag;
			session.Send(C2Map_TestRoamingMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<Chat2C_TestRPCRoamingResponse> C2Chat_TestRPCRoamingRequest(this Session session, C2Chat_TestRPCRoamingRequest C2Chat_TestRPCRoamingRequest_request)
		{
			return (Chat2C_TestRPCRoamingResponse)await session.Call(C2Chat_TestRPCRoamingRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<Chat2C_TestRPCRoamingResponse> C2Chat_TestRPCRoamingRequest(this Session session, string tag)
		{
			using var C2Chat_TestRPCRoamingRequest_request = Fantasy.C2Chat_TestRPCRoamingRequest.Create();
			C2Chat_TestRPCRoamingRequest_request.Tag = tag;
			return (Chat2C_TestRPCRoamingResponse)await session.Call(C2Chat_TestRPCRoamingRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Map_PushMessageToClient(this Session session, C2Map_PushMessageToClient C2Map_PushMessageToClient_message)
		{
			session.Send(C2Map_PushMessageToClient_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Map_PushMessageToClient(this Session session, string tag)
		{
			using var C2Map_PushMessageToClient_message = Fantasy.C2Map_PushMessageToClient.Create();
			C2Map_PushMessageToClient_message.Tag = tag;
			session.Send(C2Map_PushMessageToClient_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Map2C_PushMessageToClient(this Session session, Map2C_PushMessageToClient Map2C_PushMessageToClient_message)
		{
			session.Send(Map2C_PushMessageToClient_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Map2C_PushMessageToClient(this Session session, string tag)
		{
			using var Map2C_PushMessageToClient_message = Fantasy.Map2C_PushMessageToClient.Create();
			Map2C_PushMessageToClient_message.Tag = tag;
			session.Send(Map2C_PushMessageToClient_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<Map2C_TestTransferResponse> C2Map_TestTransferRequest(this Session session, C2Map_TestTransferRequest C2Map_TestTransferRequest_request)
		{
			return (Map2C_TestTransferResponse)await session.Call(C2Map_TestTransferRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<Map2C_TestTransferResponse> C2Map_TestTransferRequest(this Session session)
		{
			using var C2Map_TestTransferRequest_request = Fantasy.C2Map_TestTransferRequest.Create();
			return (Map2C_TestTransferResponse)await session.Call(C2Map_TestTransferRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestSendMapMessage(this Session session, C2Chat_TestSendMapMessage C2Chat_TestSendMapMessage_message)
		{
			session.Send(C2Chat_TestSendMapMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2Chat_TestSendMapMessage(this Session session, string tag)
		{
			using var C2Chat_TestSendMapMessage_message = Fantasy.C2Chat_TestSendMapMessage.Create();
			C2Chat_TestSendMapMessage_message.Tag = tag;
			session.Send(C2Chat_TestSendMapMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestRouteToRoaming(this Session session, C2G_TestRouteToRoaming C2G_TestRouteToRoaming_message)
		{
			session.Send(C2G_TestRouteToRoaming_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestRouteToRoaming(this Session session, string tag)
		{
			using var C2G_TestRouteToRoaming_message = Fantasy.C2G_TestRouteToRoaming.Create();
			C2G_TestRouteToRoaming_message.Tag = tag;
			session.Send(C2G_TestRouteToRoaming_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestRoamingToRoaming(this Session session, C2G_TestRoamingToRoaming C2G_TestRoamingToRoaming_message)
		{
			session.Send(C2G_TestRoamingToRoaming_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestRoamingToRoaming(this Session session, string tag)
		{
			using var C2G_TestRoamingToRoaming_message = Fantasy.C2G_TestRoamingToRoaming.Create();
			C2G_TestRoamingToRoaming_message.Tag = tag;
			session.Send(C2G_TestRoamingToRoaming_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ConnectRoamingResponse> C2G_LoginRoamingRequest(this Session session, C2G_LoginRoamingRequest C2G_LoginRoamingRequest_request)
		{
			return (G2C_ConnectRoamingResponse)await session.Call(C2G_LoginRoamingRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ConnectRoamingResponse> C2G_LoginRoamingRequest(this Session session)
		{
			using var C2G_LoginRoamingRequest_request = Fantasy.C2G_LoginRoamingRequest.Create();
			return (G2C_ConnectRoamingResponse)await session.Call(C2G_LoginRoamingRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_SubscribeSphereEventResponse> C2G_SubscribeSphereEventRequest(this Session session, C2G_SubscribeSphereEventRequest C2G_SubscribeSphereEventRequest_request)
		{
			return (G2C_SubscribeSphereEventResponse)await session.Call(C2G_SubscribeSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_SubscribeSphereEventResponse> C2G_SubscribeSphereEventRequest(this Session session)
		{
			using var C2G_SubscribeSphereEventRequest_request = Fantasy.C2G_SubscribeSphereEventRequest.Create();
			return (G2C_SubscribeSphereEventResponse)await session.Call(C2G_SubscribeSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PublishSphereEventResponse> C2G_PublishSphereEventRequest(this Session session, C2G_PublishSphereEventRequest C2G_PublishSphereEventRequest_request)
		{
			return (G2C_PublishSphereEventResponse)await session.Call(C2G_PublishSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PublishSphereEventResponse> C2G_PublishSphereEventRequest(this Session session)
		{
			using var C2G_PublishSphereEventRequest_request = Fantasy.C2G_PublishSphereEventRequest.Create();
			return (G2C_PublishSphereEventResponse)await session.Call(C2G_PublishSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UnsubscribeSphereEventResponse> C2G_UnsubscribeSphereEventRequest(this Session session, C2G_UnsubscribeSphereEventRequest C2G_UnsubscribeSphereEventRequest_request)
		{
			return (G2C_UnsubscribeSphereEventResponse)await session.Call(C2G_UnsubscribeSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_UnsubscribeSphereEventResponse> C2G_UnsubscribeSphereEventRequest(this Session session)
		{
			using var C2G_UnsubscribeSphereEventRequest_request = Fantasy.C2G_UnsubscribeSphereEventRequest.Create();
			return (G2C_UnsubscribeSphereEventResponse)await session.Call(C2G_UnsubscribeSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MapUnsubscribeSphereEventResponse> C2G_MapUnsubscribeSphereEventRequest(this Session session, C2G_MapUnsubscribeSphereEventRequest C2G_MapUnsubscribeSphereEventRequest_request)
		{
			return (G2C_MapUnsubscribeSphereEventResponse)await session.Call(C2G_MapUnsubscribeSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MapUnsubscribeSphereEventResponse> C2G_MapUnsubscribeSphereEventRequest(this Session session)
		{
			using var C2G_MapUnsubscribeSphereEventRequest_request = Fantasy.C2G_MapUnsubscribeSphereEventRequest.Create();
			return (G2C_MapUnsubscribeSphereEventResponse)await session.Call(C2G_MapUnsubscribeSphereEventRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestMemoryPackResponse> C2G_TestMemoryPackRequest(this Session session, C2G_TestMemoryPackRequest C2G_TestMemoryPackRequest_request)
		{
			return (G2C_TestMemoryPackResponse)await session.Call(C2G_TestMemoryPackRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestMemoryPackResponse> C2G_TestMemoryPackRequest(this Session session)
		{
			using var C2G_TestMemoryPackRequest_request = Fantasy.C2G_TestMemoryPackRequest.Create();
			return (G2C_TestMemoryPackResponse)await session.Call(C2G_TestMemoryPackRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_PlayerInfoSnapshot(this Session session, G2C_PlayerInfoSnapshot G2C_PlayerInfoSnapshot_message)
		{
			session.Send(G2C_PlayerInfoSnapshot_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_PlayerInfoSnapshot(this Session session, PlayerInfo info)
		{
			using var G2C_PlayerInfoSnapshot_message = Fantasy.G2C_PlayerInfoSnapshot.Create();
			G2C_PlayerInfoSnapshot_message.Info = info;
			session.Send(G2C_PlayerInfoSnapshot_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PropertyChangeResponse> C2G_PropertyChangeRequest(this Session session, C2G_PropertyChangeRequest C2G_PropertyChangeRequest_request)
		{
			return (G2C_PropertyChangeResponse)await session.Call(C2G_PropertyChangeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PropertyChangeResponse> C2G_PropertyChangeRequest(this Session session, PropertyType type, long delta, string reason)
		{
			using var C2G_PropertyChangeRequest_request = Fantasy.C2G_PropertyChangeRequest.Create();
			C2G_PropertyChangeRequest_request.Type = type;
			C2G_PropertyChangeRequest_request.Delta = delta;
			C2G_PropertyChangeRequest_request.Reason = reason;
			return (G2C_PropertyChangeResponse)await session.Call(C2G_PropertyChangeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_PropertyDeltaPush(this Session session, G2C_PropertyDeltaPush G2C_PropertyDeltaPush_message)
		{
			session.Send(G2C_PropertyDeltaPush_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_PropertyDeltaPush(this Session session, PropertyType type, long newAmount, string reason)
		{
			using var G2C_PropertyDeltaPush_message = Fantasy.G2C_PropertyDeltaPush.Create();
			G2C_PropertyDeltaPush_message.Type = type;
			G2C_PropertyDeltaPush_message.NewAmount = newAmount;
			G2C_PropertyDeltaPush_message.Reason = reason;
			session.Send(G2C_PropertyDeltaPush_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PropertyBatchChangeResponse> C2G_PropertyBatchChangeRequest(this Session session, C2G_PropertyBatchChangeRequest C2G_PropertyBatchChangeRequest_request)
		{
			return (G2C_PropertyBatchChangeResponse)await session.Call(C2G_PropertyBatchChangeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_PropertyBatchChangeResponse> C2G_PropertyBatchChangeRequest(this Session session, List<PropertyChangeItem> items, string reason)
		{
			using var C2G_PropertyBatchChangeRequest_request = Fantasy.C2G_PropertyBatchChangeRequest.Create();
			C2G_PropertyBatchChangeRequest_request.Items = items;
			C2G_PropertyBatchChangeRequest_request.Reason = reason;
			return (G2C_PropertyBatchChangeResponse)await session.Call(C2G_PropertyBatchChangeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_QueryAttrLedgerResponse> C2G_QueryAttrLedger(this Session session, C2G_QueryAttrLedger C2G_QueryAttrLedger_request)
		{
			return (G2C_QueryAttrLedgerResponse)await session.Call(C2G_QueryAttrLedger_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_QueryAttrLedgerResponse> C2G_QueryAttrLedger(this Session session, int kind, long sinceTs, int limit)
		{
			using var C2G_QueryAttrLedger_request = Fantasy.C2G_QueryAttrLedger.Create();
			C2G_QueryAttrLedger_request.Kind = kind;
			C2G_QueryAttrLedger_request.SinceTs = sinceTs;
			C2G_QueryAttrLedger_request.Limit = limit;
			return (G2C_QueryAttrLedgerResponse)await session.Call(C2G_QueryAttrLedger_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_SetProfileStateResponse> C2G_SetProfileStateRequest(this Session session, C2G_SetProfileStateRequest C2G_SetProfileStateRequest_request)
		{
			return (G2C_SetProfileStateResponse)await session.Call(C2G_SetProfileStateRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_SetProfileStateResponse> C2G_SetProfileStateRequest(this Session session, int skinMono, int skinMonoId, long templeDecorated)
		{
			using var C2G_SetProfileStateRequest_request = Fantasy.C2G_SetProfileStateRequest.Create();
			C2G_SetProfileStateRequest_request.SkinMono = skinMono;
			C2G_SetProfileStateRequest_request.SkinMonoId = skinMonoId;
			C2G_SetProfileStateRequest_request.TempleDecorated = templeDecorated;
			return (G2C_SetProfileStateResponse)await session.Call(C2G_SetProfileStateRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RankSubmitScoreResponse> C2G_RankSubmitScoreRequest(this Session session, C2G_RankSubmitScoreRequest C2G_RankSubmitScoreRequest_request)
		{
			return (G2C_RankSubmitScoreResponse)await session.Call(C2G_RankSubmitScoreRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RankSubmitScoreResponse> C2G_RankSubmitScoreRequest(this Session session, int rankId, long score)
		{
			using var C2G_RankSubmitScoreRequest_request = Fantasy.C2G_RankSubmitScoreRequest.Create();
			C2G_RankSubmitScoreRequest_request.RankId = rankId;
			C2G_RankSubmitScoreRequest_request.Score = score;
			return (G2C_RankSubmitScoreResponse)await session.Call(C2G_RankSubmitScoreRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RankQueryResponse> C2G_RankQueryRequest(this Session session, C2G_RankQueryRequest C2G_RankQueryRequest_request)
		{
			return (G2C_RankQueryResponse)await session.Call(C2G_RankQueryRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RankQueryResponse> C2G_RankQueryRequest(this Session session, int rankId)
		{
			using var C2G_RankQueryRequest_request = Fantasy.C2G_RankQueryRequest.Create();
			C2G_RankQueryRequest_request.RankId = rankId;
			return (G2C_RankQueryResponse)await session.Call(C2G_RankQueryRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RedeemCodeResponse> C2G_RedeemCodeRequest(this Session session, C2G_RedeemCodeRequest C2G_RedeemCodeRequest_request)
		{
			return (G2C_RedeemCodeResponse)await session.Call(C2G_RedeemCodeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RedeemCodeResponse> C2G_RedeemCodeRequest(this Session session, string code)
		{
			using var C2G_RedeemCodeRequest_request = Fantasy.C2G_RedeemCodeRequest.Create();
			C2G_RedeemCodeRequest_request.Code = code;
			return (G2C_RedeemCodeResponse)await session.Call(C2G_RedeemCodeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RenameResponse> C2G_RenameRequest(this Session session, C2G_RenameRequest C2G_RenameRequest_request)
		{
			return (G2C_RenameResponse)await session.Call(C2G_RenameRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RenameResponse> C2G_RenameRequest(this Session session, string newNickname)
		{
			using var C2G_RenameRequest_request = Fantasy.C2G_RenameRequest.Create();
			C2G_RenameRequest_request.NewNickname = newNickname;
			return (G2C_RenameResponse)await session.Call(C2G_RenameRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TarotSynthesizeResponse> C2G_TarotSynthesizeRequest(this Session session, C2G_TarotSynthesizeRequest C2G_TarotSynthesizeRequest_request)
		{
			return (G2C_TarotSynthesizeResponse)await session.Call(C2G_TarotSynthesizeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TarotSynthesizeResponse> C2G_TarotSynthesizeRequest(this Session session, int cardId)
		{
			using var C2G_TarotSynthesizeRequest_request = Fantasy.C2G_TarotSynthesizeRequest.Create();
			C2G_TarotSynthesizeRequest_request.CardId = cardId;
			return (G2C_TarotSynthesizeResponse)await session.Call(C2G_TarotSynthesizeRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestEnumMessage(this Session session, C2G_TestEnumMessage C2G_TestEnumMessage_message)
		{
			session.Send(C2G_TestEnumMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestEnumMessage(this Session session, ErrorCodeEnum code, string message, PlayerState state)
		{
			using var C2G_TestEnumMessage_message = Fantasy.C2G_TestEnumMessage.Create();
			C2G_TestEnumMessage_message.Code = code;
			C2G_TestEnumMessage_message.Message = message;
			C2G_TestEnumMessage_message.State = state;
			session.Send(C2G_TestEnumMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_WishForEnergyResponse> C2G_WishForEnergyRequest(this Session session, C2G_WishForEnergyRequest C2G_WishForEnergyRequest_request)
		{
			return (G2C_WishForEnergyResponse)await session.Call(C2G_WishForEnergyRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_WishForEnergyResponse> C2G_WishForEnergyRequest(this Session session)
		{
			using var C2G_WishForEnergyRequest_request = Fantasy.C2G_WishForEnergyRequest.Create();
			return (G2C_WishForEnergyResponse)await session.Call(C2G_WishForEnergyRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ClearPlayerDataResponse> C2G_ClearPlayerDataRequest(this Session session, C2G_ClearPlayerDataRequest C2G_ClearPlayerDataRequest_request)
		{
			return (G2C_ClearPlayerDataResponse)await session.Call(C2G_ClearPlayerDataRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_ClearPlayerDataResponse> C2G_ClearPlayerDataRequest(this Session session)
		{
			using var C2G_ClearPlayerDataRequest_request = Fantasy.C2G_ClearPlayerDataRequest.Create();
			return (G2C_ClearPlayerDataResponse)await session.Call(C2G_ClearPlayerDataRequest_request);
		}

   }
}