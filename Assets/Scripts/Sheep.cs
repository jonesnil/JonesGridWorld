﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sheep : MonoBehaviour
{
    // This class will have a tilePosition like the grass tiles, and of course a health stat.
    // it will have another Setup function TileManager will use to get it started after it 
    // creates them.

    int _health;
    int _maxHealth;
    Vector2Int _tilePos;
    Dictionary<Vector2Int, Vector2Int> currentPath;
    Vector2Int currentGoal;

    public void Setup(int x, int y, int health)
    {
        GameEvents.TileClicked += OnTileClicked;
        GameEvents.TileRightClicked += OnTileRightClicked;

        _tilePos = new Vector2Int(x, y);
        _health = health;
        _maxHealth = 10;

        StartCoroutine("AILoop");
    }

    // Also like the grass tiles it will have a coroutine where it takes an action on a loop.
    // It will consider taking an action every 2-5 seconds (not sure what will feel right yet.)
    // I think if it has low health it will make it's priority eating grass, and if not it will 
    // prioritize standing by other sheep. If it's not hungry and a sheep is close it will just
    // wander a bit. It will also wander if it has a need but nothing to fulfill it is close enough
    // to see. It will ask TileManager for information about it's environment as it needs it.
    // In this coroutine it will also check if the prequisites for breeding are met and if so that
    // will happen automatically.
    
    IEnumerator AILoop() 
    {
        // To try to make the sheep (bunnies) move where I want I gave them an array of 
        // booleans representing directions, and if the thing they're currently interested
        // in isn't in that direction they mark the spot in the array true. This stops them
        // from considering moving in that direction.

        // It has some problems (like if they want to get to grass but another sheep is in
        // the way they aren't smart enough to get around them) but it works better than
        // having them move randomly.
        bool[] ignoredDirections = new bool[] { false, false, false, false };

        // This block here checks if the conditions for spawning another sheep are good, and 
        // if they are it uses the sheep's turn to spawn another sheep. 
        List<Vector2Int> possibleSheepSpawns = GetPossibleMoves(_tilePos, ignoredDirections);
        if (AdjacentSheep(_tilePos) && _health == _maxHealth && possibleSheepSpawns.Count > 0)
        {
            this._health = 5;
            GameEvents.InvokeSheepSpawning(possibleSheepSpawns[Random.Range(0, possibleSheepSpawns.Count)]);
        }

        // This is the part that uses the dijkstra created path to reach the point you clicked.
        // Once it gets there (or if it realizes the path is broken) it stops and wanders normally.
        else if (currentPath != null) 
        {
            if (_tilePos == currentGoal || !currentPath.ContainsKey(_tilePos))
            {
                currentPath = null;
                TileManager.ResetTileColors();
            }
            else 
            {
                Vector2Int move = currentPath[_tilePos];
                GameEvents.InvokePositionChanged(_tilePos, move);

                TileManager.grassMap[move.x, move.y].occupied = true;
                TileManager.grassMap[_tilePos.x, _tilePos.y].occupied = false;

                _tilePos = move;
                TileManager.ResetTileColor(TileManager.PosToTileMap(_tilePos));
            }
        }
        // This chunk checks if the sheep has full health and if it doesn't it tells the sheep
        // to eat if it's on grass or seek out a nearby (within 3 tiles) grass tile if not. If 
        // it can't find a grass tile within three tiles that isn't occupied it wanders randomly 
        // instead (ignores no direction when moving.)
        else if (_health < _maxHealth)
        {
            GrassTile currGrass = TileManager.grassMap[_tilePos.x, _tilePos.y];

            if ((int)currGrass.state > 0)
            {
                currGrass.LowerState();
                this._health += 1;
            }

            else
            {
                Vector2Int nearbyGrass = CheckForGrass(_tilePos);
                ignoredDirections = GetIgnoredDirections(nearbyGrass);
                Move(ignoredDirections);
            }
        }

        // This bit only happens if the sheep can't breed for some reason but is at full health. 
        // Assuming the reason is because the sheep isn't by another sheep, it has them look 3 tiles
        // around and try to move toward a sheep. 
        else
        {
            Vector2Int nearbySheep = CheckForOthers(_tilePos);
            ignoredDirections = GetIgnoredDirections(nearbySheep);
            Move(ignoredDirections);
        }

        // This just makes the AI wait a second and move again.
        yield return new WaitForSeconds(1f);
        StartCoroutine("AILoop");
    }

    // This is the implementation of Dijkstra. It's not used by the sheep/bunny's pathfinding
    // normally when they wander around, but if you click a spot on the map this function gives
    // you the path to it. The guide you posted in slack about pathfinding was extremely useful
    // by the way, I followed along with it when I wrote this.
    public Dictionary<Vector2Int, Vector2Int> GetPathDijkstra(Vector2Int goal) 
    {
        currentGoal = goal;

        // I was running a little short on time and didn't want to implement a priority
        // queue so I decided to use a list of GrassTiles and just sort it by a distance
        // value I give it here, removing the first item every time I grab it. It's a little 
        // hacky but I don't think it affects the path quality at all. 
        List<GrassTile> frontier = new List<GrassTile>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> costSoFar = new Dictionary<Vector2Int, float>();

        costSoFar[_tilePos] = 0;

        TileManager.grassMap[_tilePos.x, _tilePos.y].tempDistance = 0;
        frontier.Add(TileManager.grassMap[_tilePos.x, _tilePos.y]);

        while (frontier.Count != 0) 
        {
            GrassTile currentPos = frontier[0];
            TileManager.SetTileRed(TileManager.PosToTileMap(currentPos.nodePos));
            frontier.Remove(currentPos);

            if (currentPos.nodePos == goal) 
            {
                break;
            }

            GrassTile currentTile = TileManager.grassMap[currentPos.nodePos.x, currentPos.nodePos.y];

            foreach (Vector2Int adjacentPos in currentTile.adjacentTiles.Keys) 
            {
                float newCost = costSoFar[currentPos.nodePos] + currentTile.adjacentTiles[adjacentPos];

                if (!costSoFar.ContainsKey(adjacentPos) || (newCost < costSoFar[adjacentPos])) 
                {
                    GrassTile adjacentTile = TileManager.grassMap[adjacentPos.x, adjacentPos.y];

                    // This tempDistance thing is just a hacky way to force Unity to do the work
                    // of sorting the tiles for me. I set it here every time I need to consider 
                    // distance, and then I put the tile in the list and sort it. 
                    adjacentTile.tempDistance = newCost;

                    frontier.Add(adjacentTile);
                    frontier.Sort();

                    costSoFar[adjacentPos] = adjacentTile.tempDistance;
                    cameFrom[adjacentPos] = currentPos.nodePos;
                }
            }
        }

        return cameFrom;
    }

    // This is the same as above but I added heuristics based on distance to make it A*.
    public Dictionary<Vector2Int, Vector2Int> GetPathAStar(Vector2Int goal)
    {
        currentGoal = goal;

        // I was running a little short on time and didn't want to implement a priority
        // queue so I decided to use a list of GrassTiles and just sort it by a distance
        // value I give it here, removing the first item every time I grab it. It's a little 
        // hacky but I don't think it affects the path quality at all. 
        List<GrassTile> frontier = new List<GrassTile>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> costSoFar = new Dictionary<Vector2Int, float>();

        costSoFar[_tilePos] = 0;

        TileManager.grassMap[_tilePos.x, _tilePos.y].tempDistance = 0;
        frontier.Add(TileManager.grassMap[_tilePos.x, _tilePos.y]);

        while (frontier.Count != 0)
        {
            GrassTile currentPos = frontier[0];
            TileManager.SetTileRed(TileManager.PosToTileMap(currentPos.nodePos));
            frontier.Remove(currentPos);

            if (currentPos.nodePos == goal)
            {
                break;
            }

            GrassTile currentTile = TileManager.grassMap[currentPos.nodePos.x, currentPos.nodePos.y];

            foreach (Vector2Int adjacentPos in currentTile.adjacentTiles.Keys)
            {
                // Here are the heuristic A* changes.
                float heuristic = Vector2.Distance(adjacentPos, goal);
                float newCost = costSoFar[currentPos.nodePos] + currentTile.adjacentTiles[adjacentPos] + heuristic;

                if (!costSoFar.ContainsKey(adjacentPos) || (newCost < costSoFar[adjacentPos]))
                {
                    GrassTile adjacentTile = TileManager.grassMap[adjacentPos.x, adjacentPos.y];

                    // This tempDistance thing is just a hacky way to force Unity to do the work
                    // of sorting the tiles for me. I set it here every time I need to consider 
                    // distance, and then I put the tile in the list and sort it. 
                    adjacentTile.tempDistance = newCost;

                    frontier.Add(adjacentTile);
                    frontier.Sort();

                    costSoFar[adjacentPos] = adjacentTile.tempDistance;
                    cameFrom[adjacentPos] = currentPos.nodePos;
                }
            }
        }

        return cameFrom;
    }

    // This listens to an event. The collider for telling when you clicked is on the TileManager,
    // so the TileManager just calls this as soon as you click and the sheep class does the work
    // of making the path and displaying it on screen/making the sheep follow it. 
    // This listens to an event. The collider for telling when you clicked is on the TileManager,
    // so the TileManager just calls this as soon as you click and the sheep class does the work
    // of making the path and displaying it on screen/making the sheep follow it. 
    void OnTileClicked(object sender, Vector2IntEventArgs args)
    {
        Vector2Int destination = args.positionPayload;
        Dictionary<Vector2Int, Vector2Int> path = GetPathAStar(destination);
        Dictionary<Vector2Int, Vector2Int> reversedPath = new Dictionary<Vector2Int, Vector2Int>();

        Vector2Int current = destination;
        while (current != this._tilePos) 
        {
            TileManager.SetTileBlue(TileManager.PosToTileMap(current));

            if (path.ContainsKey(current))
            {
                reversedPath[path[current]] = current;
                current = path[current];
            }
            else
                break;
        }

        currentPath = reversedPath;
    }

    // This makes it so when you right click you can see what Dijkstra's path would be. It's the same
    // as above other than that.
    void OnTileRightClicked(object sender, Vector2IntEventArgs args)
    {
        Vector2Int destination = args.positionPayload;
        Dictionary<Vector2Int, Vector2Int> path = GetPathDijkstra(destination);
        Dictionary<Vector2Int, Vector2Int> reversedPath = new Dictionary<Vector2Int, Vector2Int>();

        Vector2Int current = destination;
        while (current != this._tilePos)
        {
            TileManager.SetTileBlue(TileManager.PosToTileMap(current));

            if (path.ContainsKey(current))
            {
                reversedPath[path[current]] = current;
                current = path[current];
            }
            else
                break;
        }

        currentPath = reversedPath;
    }

    // This uses helper functions to figure out which moves are in the visible grid/
    // not occupied and then picks a move based on that. It also considers which directions
    // the sheep doesn't want to move (ignoredDirections) and doesn't move that way. If that
    // process leaves no moves available the sheep will do nothing on their turn.
    public void Move(bool[] ignoredDirections) 
    {
        List<Vector2Int> moves = GetPossibleMoves(_tilePos, ignoredDirections);

        if (moves.Count != 0)
        {
            Vector2Int move = moves[Random.Range(0, moves.Count)];
            GameEvents.InvokePositionChanged(_tilePos, move);

            TileManager.grassMap[move.x, move.y].occupied = true;
            TileManager.grassMap[_tilePos.x, _tilePos.y].occupied = false;

            _tilePos = move;
        }
    }

    // This function checks the four possible moves for a sheep against the grid and
    // returns acceptable options (and finally uses the ignoredDirections array to
    // weed out those directions accordingly.)
    List<Vector2Int> GetPossibleMoves(Vector2Int pos, bool[] ignoredDirections)
    {
        List<Vector2Int> output = new List<Vector2Int>();
        List<Vector2Int> checkMoves = new List<Vector2Int>();
        if (!ignoredDirections[0])
            checkMoves.Add(new Vector2Int(pos.x, pos.y - 1));
        if (!ignoredDirections[1])
            checkMoves.Add(new Vector2Int(pos.x, pos.y + 1));
        if (!ignoredDirections[2])
            checkMoves.Add(new Vector2Int(pos.x - 1, pos.y));
        if (!ignoredDirections[3])
            checkMoves.Add(new Vector2Int(pos.x + 1, pos.y));

        int widthLength = TileManager.grassMap.GetLength(0);
        int heightLength = TileManager.grassMap.GetLength(1);

        foreach (Vector2Int move in checkMoves)
        {
            if (MoveWorks(move, widthLength, heightLength) && !Occupied(move))
            {
                output.Add(move);
            }
        }

        return output;
    }

    // This function returns true if there's a sheep directly adjacent to this
    // sheep and false if there isn't. It's used to decide if a sheep can breed.
    bool AdjacentSheep(Vector2Int pos)
    {
        List<Vector2Int> checkSpots = new List<Vector2Int>();
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y));

        int widthLength = TileManager.grassMap.GetLength(0);
        int heightLength = TileManager.grassMap.GetLength(1);

        foreach (Vector2Int spot in checkSpots)
        {
            if (MoveWorks(spot, widthLength, heightLength) && Occupied(spot))
            {
                return true;
            }
        }

        return false;
    }

    // This function returns the position of another sheep within 3 tiles
    // if it exists. Otherwise it returns a Vector2 containing (420, 420) to let 
    // the program know none was found. 
    Vector2Int CheckForOthers(Vector2Int pos) 
    {
        List<Vector2Int> checkSpots = new List<Vector2Int>();
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y));
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 2));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 2, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 3));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 3));
        checkSpots.Add(new Vector2Int(pos.x - 3, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 3, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 2, pos.y + 2));
        checkSpots.Add(new Vector2Int(pos.x + 2, pos.y - 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y + 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y - 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y - 2));

        int widthLength = TileManager.grassMap.GetLength(0);
        int heightLength = TileManager.grassMap.GetLength(1);

        foreach (Vector2Int spot in checkSpots)
        {
            if (MoveWorks(spot, widthLength, heightLength) && Occupied(spot))
            {
                return spot;
            }
        }

        return new Vector2Int(420,420);

    }


    // This function returns the position of unoccupied grass within 3 tiles
    // if it exists. Otherwise it returns a Vector2 containing (420, 420) to let 
    // the program know none was found. 
    Vector2Int CheckForGrass(Vector2Int pos)
    {
        List<Vector2Int> checkSpots = new List<Vector2Int>();
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y));
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 2));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 2, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x + 1, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y + 1));
        checkSpots.Add(new Vector2Int(pos.x - 1, pos.y - 1));
        checkSpots.Add(new Vector2Int(pos.x, pos.y + 3));
        checkSpots.Add(new Vector2Int(pos.x, pos.y - 3));
        checkSpots.Add(new Vector2Int(pos.x - 3, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 3, pos.y));
        checkSpots.Add(new Vector2Int(pos.x + 2, pos.y + 2));
        checkSpots.Add(new Vector2Int(pos.x + 2, pos.y - 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y + 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y - 2));
        checkSpots.Add(new Vector2Int(pos.x - 2, pos.y - 2));

        int widthLength = TileManager.grassMap.GetLength(0);
        int heightLength = TileManager.grassMap.GetLength(1);

        foreach (Vector2Int spot in checkSpots)
        {
            if (MoveWorks(spot, widthLength, heightLength) && Grass(spot) && !Occupied(spot))
            {
                return spot;
            }
        }

        return new Vector2Int(420, 420);

    }

    // This is a helper function which takes a target position (like another sheep
    // or some grass) and makes an array based on this sheep's position to tell it
    // which directions not to go. That array is passed to the Move function later
    // to give it some guidance.
    bool[] GetIgnoredDirections(Vector2Int target) 
    {
        bool[] output = new bool[4];
        output[0] = false;
        output[1] = false;
        output[2] = false;
        output[3] = false;

        if (target.x != 420)
        {
            if (target.y > _tilePos.y)
            {
                output[0] = true;
            }

            if (target.y < _tilePos.y)
            {
                output[1] = true;
            }

            if (target.x > _tilePos.x)
            {
                output[2] = true;
            }

            if (target.x < _tilePos.x)
            {
                output[3] = true;
            }
        }

        return output;
    }

    // This function returns true if a position is actually on the visible grid and false
    // if it's not. It's used by a lot of functions here to prevent trying to grab something
    // that's not on the grid and getting an error. It also prevents sheep from moving off 
    // the screen.
    bool MoveWorks(Vector2Int move, int widthLength, int heightLength)
    {
        if (move.x >= 0 && move.x < widthLength && move.y >= 0 && move.y < heightLength && !TileManager.grassMap[move.x,move.y].isIce)
            return true;
        else
            return false;
    }

    // This function returns true if there is a sheep in a given position and false if there
    // isn't. It's used to stop sheep from colliding and to find sheep to chase. It also is
    // used to determine if a sheep is close enough for breeding.
    bool Occupied(Vector2Int move) 
    {
        return TileManager.grassMap[move.x, move.y].occupied;
    }

    // This function returns true if there is any edible grass on a given space and false if 
    // there isn't. It's used to tell sheep if they can eat and help them hunt grass.
    bool Grass(Vector2Int move) 
    {
        if((int) TileManager.grassMap[move.x, move.y].state > 0)
            return true;

        else
            return false;
    }
}
