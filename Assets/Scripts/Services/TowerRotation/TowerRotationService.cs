using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TowerDirection
{
    North,
    East,
    South,
    West
}

public enum TowerCorners
{
    NorthEast,
    SouthEast,
    SouthWest,
    NorthWest
}

public class TowerRotationService : Service, IService
{
    public delegate void OnBeforeTurnDel();
    public event OnBeforeTurnDel OnBeforeTurn;
    
    public delegate void OnTurnDel();
    public event OnTurnDel OnTurn;
    
    public delegate void OnPostTurnDel();
    public event OnPostTurnDel OnPostTurn;
    
    public Vector3 ROTATION_ORIGIN { get; private set; }
    public float ROTATION_AMOUNT { get; private set; }
    public float ROTATION_TIME { get; private set; }
    public float ROTATION_PROGRESSION { get; private set; }
    public float ROTATION_THIS_FRAME { get; private set; }
    public TowerDirection TOWER_DIRECTION { get; private set; }
    public bool ROTATING { get; private set; }
    
    private CoroutineRunner _coroutineRunner;
    
    
    public void Rotate(TowerCorners towerCorner, Vector3 rotationOrigin, float turnTime)
    {
        if (_coroutineRunner != null)
        {
            return;
        }
        
        float direction = 0f;
        switch (towerCorner)
        {
            case TowerCorners.NorthEast : direction = TOWER_DIRECTION is TowerDirection.North ? 1f : -1f;
                break;
            case TowerCorners.SouthEast : direction = TOWER_DIRECTION is TowerDirection.East ? 1f : -1f;
                break;
            case TowerCorners.SouthWest : direction = TOWER_DIRECTION is TowerDirection.South ? 1f : -1f;
                break;
            case TowerCorners.NorthWest : direction = TOWER_DIRECTION is TowerDirection.West ? 1f : -1f;
                break;
        }
        
        ROTATION_ORIGIN = rotationOrigin;
        ROTATION_AMOUNT = direction * 90f;
        ROTATION_TIME = turnTime;
        ROTATING = true;
        DoBeforeTurn();
        DoTurn();
        
        _coroutineRunner = new GameObject("CoroutineRunner").AddComponent<CoroutineRunner>();
        //DontDestroyOnLoad(_coroutineRunner.gameObject);
        //_turningCoroutine ??= StartCoroutine(TurnWait(direction, rotationOrigin));
    }
    
    public IEnumerator TurnWait(float direction, Vector3 origin)
    {
        var waitTime = 0f;
        var interpolatedTime = 0f;
        while (waitTime < ROTATION_TIME)
        {
            if (waitTime + Time.deltaTime > ROTATION_TIME)
            {
                waitTime = ROTATION_TIME;
            }
            else
            {
                waitTime += Time.deltaTime;
            }

            ROTATION_PROGRESSION = Mathf.InverseLerp(0, ROTATION_TIME, waitTime);

            ROTATION_THIS_FRAME = ROTATION_AMOUNT * Time.deltaTime * (1f/ ROTATION_TIME);
            DoTurn();
            yield return 0;
        }
        
        UpdateFace(direction);
        _internalCoroutine = null;
        Object.Destroy(_coroutineRunner);
        _coroutineRunner = null;
        ROTATION_PROGRESSION = 0f;
        DoPostTurn();
        ROTATING = false;
    }
    
    private void UpdateFace(float direction)
    {
        if (direction > 0)
        {
            TOWER_DIRECTION = TOWER_DIRECTION switch
            {
                TowerDirection.North => TowerDirection.East,
                TowerDirection.East => TowerDirection.South,
                TowerDirection.South => TowerDirection.West,
                TowerDirection.West => TowerDirection.North,
                _ => TOWER_DIRECTION
            };
        }
        else
        {
            TOWER_DIRECTION = TOWER_DIRECTION switch
            {
                TowerDirection.North => TowerDirection.West,
                TowerDirection.East => TowerDirection.North,
                TowerDirection.South => TowerDirection.East,
                TowerDirection.West => TowerDirection.South,
                _ => TOWER_DIRECTION
            };
        }
    }

    public void DoBeforeTurn()
    {
        OnBeforeTurn?.Invoke();
    }
    
    public void DoTurn()
    {
        OnTurn?.Invoke();
    }
    
    public void DoPostTurn()
    {
        OnPostTurn?.Invoke();
    }
}