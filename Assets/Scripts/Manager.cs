using UnityEngine;
using System.Collections;

public class Manager : MonoBehaviour {
	
	public int playfieldWidth = 10;
	public int playfieldHeight = 13;
	public int maxBrickSize = 5;
	public float brickNormalSpeed = 2.0f;
	public float brickDropSpeed = 30.0f;
	public float brickMoveDelay = 0.1f;
	public int rowsClearedToSpeedup = 10;
	public float speedupAmount = 0.5f;

	public GameObject[] existentBricks;
	public Transform cube;
	public Transform leftWall;
	public Transform rightWall;

	private int totalFieldWidth;
	private int totalFieldHeight;
	private bool[,] playfield;
	private Transform[] cubeReferences;
	private int[] cubePositions;
	private int rowsCleared = 0;
	
	public static Manager use;

	void Start () {
		if (!Manager.use) {
			Manager.use = this;	// Get a reference to this script, which is a static variable so it's used as a singleton
		}
		
		// Make the field larger to fit walls.
		this.totalFieldWidth = this.playfieldWidth + this.maxBrickSize * 2;
		
		// Make the field taller to fit "spawn area".
		this.totalFieldHeight = this.playfieldHeight + this.maxBrickSize;
		
		this.playfield = new bool[this.totalFieldWidth, this.totalFieldHeight];
		
		// Make the "walls" and "floor" in the array.
		// 		true = cube of a brick
		// 		false = open space
		// This way we don't need special logic to deal with the bottom or edges of the playing field,
		// since blocks will collide with the walls/floor the same as with other blocks
		// Also, we use 0 = bottom and fieldHeight-1 = top, so that positions in the array match positions in 3D space
		
		// Make walls.
		for (int i = 0; i < this.totalFieldHeight; i++) {
			for (int j = 0; j < this.maxBrickSize; j++) {
				this.playfield[j, i] = true;
				this.playfield[this.totalFieldWidth-1-j, i] = true;
			}
		}
		
		// Make floor.
		for (int i = 0; i < this.totalFieldWidth; i++) {
			this.playfield[i, 0] = true;
		}
		
		// Position stuff in the scene so it looks right regardless of what sizes are entered for the playing field
		// (Though the camera would have to be moved back for larger sizes)
		this.leftWall.position = new Vector3(this.maxBrickSize - 0.5f, this.leftWall.position.y, this.leftWall.position.z);
		this.rightWall.position = new Vector3(this.totalFieldWidth - this.maxBrickSize + 0.5f, this.rightWall.position.y, this.rightWall.position.z);
		Camera.main.transform.position = new Vector3(this.totalFieldWidth / 2, this.totalFieldHeight / 2, -16.5f);
		
		this.cubeReferences = new Transform[this.totalFieldWidth * this.totalFieldHeight];
		this.cubePositions = new int[this.totalFieldWidth * this.totalFieldHeight];

		this.SpawnBrick();
	}
	
	private void SpawnBrick () {
		Instantiate(this.existentBricks[Random.Range(0, this.existentBricks.Length)]);
	}
	
	public int TotalFieldHeight () {
		return this.totalFieldHeight;
	}
	
	public int TotalFieldWidth () {
		return this.totalFieldWidth;
	}
	
	// See if the block matrix would overlap existing blocks in the playing field
	// (Check from bottom-up, since in general gameplay usage it's a bit more efficient that way)
	public bool CheckBrickCollision (bool[,] brickMatrix, int xPos, int yPos) {
		int size = brickMatrix.GetLength(0);

		for (int y = size-1; y >= 0; y--) {
			for (int x = 0; x < size; x++) {
				if (brickMatrix[x, y] && this.playfield[xPos+x, yPos-y]) {
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
	public IEnumerator SetBrick (bool[,] blockMatrix, int xPos, int yPos) {
		int size = blockMatrix.GetLength(0);
		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {	
				if (blockMatrix[x, y]) {
					Instantiate(cube, new Vector3(xPos+x, yPos-y, 0.0f), Quaternion.identity);
					this.playfield[xPos+x, yPos-y] = true;
				}
			}
		}
		
		// Nada roda depois dessa funçao.
		this.SpawnBrick();
		yield return StartCoroutine(this.CheckRows(yPos - size, size));
		
	}
	
	private IEnumerator CheckRows (int yStart, int size) {
		yield return 0;	// Wait a frame for block to be destroyed so we don't include those cubes
		
		if (yStart < 1) {
			yStart = 1;	// Make sure to start above the floor
		}
		
		int lastX = 0;
		for (int y = yStart; y < yStart+size; y++) {
			for (int x = this.maxBrickSize; x < this.totalFieldWidth - this.maxBrickSize; x++) { // We don't need to check the walls
				if (!this.playfield[x, y]) break;
				lastX = x;
			}
			lastX++;
			
			// If the loop above completed, then x will equal fieldWidth-maxBlockSize, which means the row was completely filled in
			if (lastX == this.totalFieldWidth - this.maxBrickSize) {
				yield return StartCoroutine(CollapseRows(y));
				y--; // We want to check the same row again after the collapse, in case there was more than one row filled in
			}
		}
		
	}
	
	private IEnumerator CollapseRows (int yStart) {
		// Move rows down in array, which effectively deletes the current row (yStart)
		for (int y = yStart; y < this.totalFieldHeight-1; y++) {
			for (int x = this.maxBrickSize; x < this.totalFieldWidth - this.maxBrickSize; x++) {
				this.playfield[x, y] = this.playfield[x, y+1];
			}
		}

		// Make sure top line is cleared
		for (int x = this.maxBrickSize; x < this.totalFieldWidth - this.maxBrickSize; x++) {
			this.playfield[x, this.totalFieldHeight-1] = false;
		}
		
		// Destroy on-screen cubes on the deleted row, and store references to cubes that are above it
		GameObject[] cubes = GameObject.FindGameObjectsWithTag("Cube");
		int cubesToMove = 0;

		foreach (GameObject cube in cubes) {
			if (cube.transform.position.y > yStart) {
				this.cubePositions[cubesToMove] = (int) cube.transform.position.y;
				this.cubeReferences[cubesToMove++] = cube.transform;
			} else if (cube.transform.position.y == yStart) {
				Destroy(cube);
			}
		}

		// Move the appropriate cubes down one square
		// The third parameter in Mathf.Lerp is clamped to 1.0, which makes the transform.position.y be positioned exactly when done,
		// which is important for the game logic (see the code just above)
		float t = 0.0f;
		while (t <= 1.0f) {
			t += Time.deltaTime * 5.0f;
			for (int i = 0; i < cubesToMove; i++) {
				this.cubeReferences[i].position = new Vector3(
					this.cubeReferences[i].position.x,
					Mathf.Lerp((float) this.cubePositions[i], (float) (this.cubePositions[i]-1), t),
					this.cubeReferences[i].position.z
				);
			}
			yield return 0;
		}
		
		// Make blocks drop faster when enough rows are cleared
		if (++this.rowsCleared == this.rowsClearedToSpeedup) {
			this.brickNormalSpeed += this.speedupAmount;
			this.rowsCleared = 0;
		}
	}

	public void GameOver () {
		Debug.Log ("Game Over!");
	}

	// Prints the state of the field array, for debugging
	public void PrintField () {
		string fieldChars = "";

		for (int y = this.totalFieldHeight-1; y >= 0; y--) {
			for (int x = 0; x < this.totalFieldWidth; x++) {
				fieldChars += this.playfield[x, y] ? "1" : "0";
			}
			fieldChars += "\n";
		}
		Debug.Log (fieldChars);
	}
}