var _fieldWidth = 10;
var _fieldHeight = 13;
var maxBlockSize = 5;
var blockNormalSpeed = 2.0;
var blockDropSpeed = 30.0;
var blockMoveDelay = .1;
var rowsClearedToSpeedup = 10;
var speedupAmount = .5;
var blocks : GameObject[];
var cube : Transform;
var leftWall : Transform;
var rightWall : Transform;

private var fieldWidth : int;
private var fieldHeight : int;
private var field : boolean[,];
private var cubeReferences : Transform[];
private var cubePositions : int[];
private var rowsCleared = 0;
static var use : Manager;

function Start () {
	if (!use) {
		use = this;	// Get a reference to this script, which is a static variable so it's used as a singleton
	}
	else {
		Debug.LogError ("Only one instance of this script is allowed");
		return;
	}
	
	// Make the "real" width/height larger, to account for borders and space at the top for initial block placement
	fieldWidth = _fieldWidth + maxBlockSize*2;
	fieldHeight = _fieldHeight + maxBlockSize;
	field = new boolean[fieldWidth, fieldHeight];
	
	// Make the "walls" and "floor" in the array...we use true = block, and false = open space
	// This way we don't need special logic to deal with the bottom or edges of the playing field,
	// since blocks will collide with the walls/floor the same as with other blocks
	// Also, we use 0 = bottom and fieldHeight-1 = top, so that positions in the array match positions in 3D space
	for (i = 0; i < fieldHeight; i++) {
		for (j = 0; j < maxBlockSize; j++) {
			field[j, i] = true;
			field[fieldWidth-1-j, i] = true;
		}
	}
	for (i = 0; i < fieldWidth; i++) {
		field[i, 0] = true;
	}
	
	// Position stuff in the scene so it looks right regardless of what sizes are entered for the playing field
	// (Though the camera would have to be moved back for larger sizes)
	leftWall.position.x = maxBlockSize-.5;
	rightWall.position.x = fieldWidth-maxBlockSize+.5;
	Camera.main.transform.position = Vector3(fieldWidth/2, fieldHeight/2, -16.0);
	
	cubeReferences = new Transform[fieldWidth * fieldHeight];
	cubePositions = new int[fieldWidth * fieldHeight];
	
	SpawnBlock();
}

function SpawnBlock () {
	Instantiate (blocks[Random.Range(0, blocks.Length)]);
}

function FieldHeight () : int {
	return fieldHeight;
}

function FieldWidth () : int {
	return fieldWidth;
}

// See if the block matrix would overlap existing blocks in the playing field
// (Check from bottom-up, since in general gameplay usage it's a bit more efficient that way)
function CheckBlock (blockMatrix : boolean[,], xPos : int, yPos : int) : boolean {
	var size = blockMatrix.GetLength(0);
	for (y = size-1; y >= 0; y--) {
		for (x = 0; x < size; x++) {
			if (blockMatrix[x, y] && field[xPos+x, yPos-y]) {
				return true;
			}
		}
	}
	return false;
}

// Make on-screen cubes from position in array when the block is stopped from falling any more
// Just using DetachChildren isn't feasible because the child cubes can be in different orientations,
// which can mess up their position on the Y axis, which we need to be consistent in CollapseRow
// Also write the block matrix into the corresponding location in the playing field
function SetBlock (blockMatrix : boolean[,], xPos : int, yPos : int) {
	var size = blockMatrix.GetLength(0);
	for (y = 0; y < size; y++) {
		for (x = 0; x < size; x++) {	
			if (blockMatrix[x, y]) {
				Instantiate (cube, Vector3(xPos+x, yPos-y, 0.0), Quaternion.identity);
				field[xPos+x, yPos-y] = true;
			}
		}
	}
	yield CheckRows (yPos - size, size);
	SpawnBlock();
}

function CheckRows (yStart : int, size : int) {
	yield;	// Wait a frame for block to be destroyed so we don't include those cubes
	if (yStart < 1) yStart = 1;	// Make sure to start above the floor
	for (y = yStart; y < yStart+size; y++) {
		for (x = maxBlockSize; x < fieldWidth-maxBlockSize; x++) { // We don't need to check the walls
			if (!field[x, y]) break;
		}
		// If the loop above completed, then x will equal fieldWidth-maxBlockSize, which means the row was completely filled in
		if (x == fieldWidth-maxBlockSize) {
			yield CollapseRows (y);
			y--; // We want to check the same row again after the collapse, in case there was more than one row filled in
		}
	}
}

function CollapseRows (yStart : int) {
	// Move rows down in array, which effectively deletes the current row (yStart)
	for (y = yStart; y < fieldHeight-1; y++) {
		for (x = maxBlockSize; x < fieldWidth-maxBlockSize; x++) {
			field[x, y] = field[x, y+1];
		}
	}
	// Make sure top line is cleared
	for (x = maxBlockSize; x < fieldWidth-maxBlockSize; x++) {
		field[x, fieldHeight-1] = false;
	}
	
	// Destroy on-screen cubes on the deleted row, and store references to cubes that are above it
	var cubes = gameObject.FindGameObjectsWithTag("Cube");
	var cubesToMove = 0;
	for (cube in cubes) {
		if (cube.transform.position.y > yStart) {
			cubePositions[cubesToMove] = cube.transform.position.y;
			cubeReferences[cubesToMove++] = cube.transform;
		}
		else if (cube.transform.position.y == yStart) {
			Destroy(cube);
		}
	}
	// Move the appropriate cubes down one square
	// The third parameter in Mathf.Lerp is clamped to 1.0, which makes the transform.position.y be positioned exactly when done,
	// which is important for the game logic (see the code just above)
	var t = 0.0;
	while (t <= 1.0) {
		t += Time.deltaTime * 5.0;
		for (i = 0; i < cubesToMove; i++) {
			cubeReferences[i].position.y = Mathf.Lerp (cubePositions[i], cubePositions[i]-1, t);
		}
		yield;
	}
	
	// Make blocks drop faster when enough rows are cleared
	if (++rowsCleared == rowsClearedToSpeedup) {
		blockNormalSpeed += speedupAmount;
		rowsCleared = 0;
	}
}

function GameOver () {
	Debug.Log ("Game Over!");
}

// Prints the state of the field array, for debugging
function PrintField () {
	var fieldChars = "";
	for (y = fieldHeight-1; y >= 0; y--) {
		for (x = 0; x < fieldWidth; x++) {
			fieldChars += field[x, y]? "1" : "0";
		}
		fieldChars += "\n";
	}
	Debug.Log (fieldChars);
}