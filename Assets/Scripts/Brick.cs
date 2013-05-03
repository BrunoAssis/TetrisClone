using UnityEngine;
using System.Collections;

public class Brick : MonoBehaviour {
	
	public string[] brickShape;
	
	private bool[,] cubeMatrix;
	private float fallSpeed;
	private int yPosition;
	private int xPosition;
	private int size;
	private float halfSizeFloat;
	private bool dropped = false;
	
	IEnumerator Start() {
		// Sanity checking
		this.size = this.brickShape.Length;
		
		int width = this.brickShape[0].Length;
		
		if (this.size < 2) {
			Debug.LogError ("Bricks must have at least two lines");
			yield break;
		}
		
		if (width != size) {
			Debug.LogError ("Brick width and height must be the same");
			yield break;
		}
		
		if (this.size > Manager.use.maxBrickSize) {
			Debug.LogError ("Brick must not be larger than " + Manager.use.maxBrickSize);
			yield break;
		}
		
		for (int i = 1; i < size; i++) {
			if (this.brickShape[i].Length != this.brickShape[i-1].Length) {
				Debug.LogError ("All lines in the brick must be the same length");
				yield break;
			}
		}

		this.halfSizeFloat = this.size * 0.5f; // halfSize is an integer for the array, but we need a float for positioning the on-screen cubes (for odd sizes)
		
		// Convert brick string array from the inspector to a boolean 2D array for easier usage
		this.cubeMatrix = new bool[size, size];

		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {
				// This [0] converts "1" string to char.
				if (this.brickShape[y][x] == "1"[0]) {
					this.cubeMatrix[x, y] = true;
					Transform brick = (Transform) Instantiate(Manager.use.cube, new Vector3(x - this.halfSizeFloat, (this.size - y) + this.halfSizeFloat - size, 0.0f), Quaternion.identity);
					brick.parent = this.transform;
				}
			}
		}
		
		// For bricks with even sizes, we just add 0, but odd sizes need .5 added to the position to work right
		this.transform.position = new Vector3(Manager.use.TotalFieldWidth() / 2 + (this.size % 2 == 0 ? 0.0f : 0.5f), this.transform.position.y, this.transform.position.z);

		this.xPosition = (int) (this.transform.position.x - this.halfSizeFloat);
		this.yPosition = Manager.use.TotalFieldHeight() - 1;

		this.transform.position = new Vector3(this.transform.position.x, this.yPosition - this.halfSizeFloat, this.transform.position.z);
		this.fallSpeed = Manager.use.brickNormalSpeed;
		
		// Check to see if this brick would overlap existing bricks, in which case the game is over
		if (Manager.use.CheckBrickCollision(this.cubeMatrix, this.xPosition, this.yPosition)) {
			Manager.use.GameOver();
			yield break;
		}
		
		StartCoroutine(CheckInput());
		yield return StartCoroutine(Delay((1.0f / Manager.use.brickNormalSpeed) * 2.0f));
		StartCoroutine(Fall());
	}
	
	private IEnumerator CheckInput () {
		while (true) {
			float input = Input.GetAxis("Horizontal");
			if (input < 0.0f) {
				yield return StartCoroutine(this.MoveHorizontal(-1));
			} else if (input > 0.0f) {
				yield return StartCoroutine(this.MoveHorizontal(1));
			}
	
			if (Input.GetButtonDown("Rotate")) {
				this.RotateBrick();
			}
	
			if (Input.GetButtonDown("Drop")) {
				this.fallSpeed = Manager.use.brickDropSpeed;
				this.dropped = true;
				break;
			}
			
			if (Input.GetButtonDown("Camera")) {
				Manager.use.ChangeCamera();
			}
			
			yield return 0;
		}
	}

	// This is used instead of WaitForSeconds, so that the delay can be cut short if player hits the drop button
	private IEnumerator Delay (float seconds) {
		float t = 0.0f;
		while (t <= seconds && !dropped) {
			t += Time.deltaTime;
			yield return 0;
		}
	}
	
	private IEnumerator Fall () {
		while (true) {
			// Check to see if brick would collide if moved down one row
			this.yPosition--;
			if (Manager.use.CheckBrickCollision(this.cubeMatrix, this.xPosition, this.yPosition)) {
				StartCoroutine(Manager.use.SetBrick(this.cubeMatrix, this.xPosition, this.yPosition+1));
				Destroy(this.gameObject);
				break;
			}
				
			// Make on-screen brick fall down 1 square
			for (float i = (float) this.yPosition + 1.0f; i > this.yPosition; i -= Time.deltaTime * this.fallSpeed) {
				this.transform.position = new Vector3(this.transform.position.x, i - this.halfSizeFloat, this.transform.position.z);
				yield return 0;
			}
		}
	}
	
	private IEnumerator MoveHorizontal (int dir) {
		// Check to see if brick could be moved in the desired direction
		if (!Manager.use.CheckBrickCollision(this.cubeMatrix, this.xPosition + dir, this.yPosition)) {
			this.transform.position += new Vector3(dir, 0.0f, 0.0f);
			this.xPosition += dir;
			yield return new WaitForSeconds(Manager.use.brickMoveDelay);
		}
	}
	
	private void RotateBrick () {
		// Rotate matrix 90° to the right and store the results in a temporary matrix
		bool[,] tempMatrix = new bool[this.size, this.size];
		for (int y = 0; y < size; y++) {
			for (int x = 0; x < size; x++) {
				tempMatrix[y, x] = this.cubeMatrix[x, (this.size - 1) - y];
			}
		}
		
		// If the rotated brick doesn't overlap existing bricks, copy the rotated matrix back and rotate on-screen brick to match
		if (!Manager.use.CheckBrickCollision(tempMatrix, this.xPosition, this.yPosition)) {
			System.Array.Copy (tempMatrix, this.cubeMatrix, this.size * this.size);
			this.transform.Rotate(Vector3.forward * -90.0f);
		}
	}
}