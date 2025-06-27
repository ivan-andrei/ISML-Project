using UnityEngine;
using System.Linq;

public class RoomCameraController : MonoBehaviour
{
    public static event System.Action<Vector2Int, Vector2Int> OnPlayerEnteredNewRoom;

    [Header("References")]
    public Transform player;

    [Header("Camera Settings")]
    public float smoothTime = 0.25f;

    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;
    private Vector2Int currentRoomGridPosition;

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("Player transform is not assigned in the RoomCameraController!", this);
            this.enabled = false;
            return;
        }

        currentRoomGridPosition = GetGridPosition(player.position);
        
        targetPosition = GetRoomCenter(currentRoomGridPosition);

        transform.position = targetPosition;
    }

    void LateUpdate()
    {
        if (DungeonGenerator.roomCellSize == Vector2Int.zero) return;

        Vector2Int playerGridPos = GetGridPosition(player.position);

        if (playerGridPos != currentRoomGridPosition)
        {
            currentRoomGridPosition = playerGridPos;
            targetPosition = GetRoomCenter(currentRoomGridPosition);

            OnPlayerEnteredNewRoom?.Invoke(playerGridPos, DungeonGenerator.roomCellSize);

            if (DungeonGenerator.Instance != null)
            {
                DungeonGenerator.Instance.SpawnEnemiesInRoom(playerGridPos);
            }
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    private Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / DungeonGenerator.roomCellSize.x),
            Mathf.RoundToInt(worldPosition.y / DungeonGenerator.roomCellSize.y)
        );
    }

    private Vector3 GetRoomCenter(Vector2Int gridPosition)
    {
        Vector3 roomCenter = new Vector3(
            gridPosition.x * DungeonGenerator.roomCellSize.x,
            gridPosition.y * DungeonGenerator.roomCellSize.y,
            transform.position.z 
        );
        return roomCenter;
    }
}