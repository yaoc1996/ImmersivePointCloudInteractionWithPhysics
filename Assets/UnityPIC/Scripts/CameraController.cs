using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour {
    public Camera Camera;
    public float normalSpeed = 100;
    public float minSpeed = 100;
    public float maxSpeed = 1000;

    public Text text;
    GameObject player;

    void Start() {
        //Hide the cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        player = GameObject.Find("Player");
    }

    void Update() {
        if (Input.GetKey(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            //enabled = false;
        }
        
        if (Input.GetMouseButtonDown(0)) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (text != null)
            text.text = string.Format("Camera Speed (Mouse Scroll, Shift to speed): {0}", normalSpeed);
    }

    private Queue<Vector3> _rotHistory = new Queue<Vector3>();
    private Queue<Vector3> _posHistory = new Queue<Vector3>();
    private Queue<float> _speedHistory = new Queue<float>();
    private Vector3 _runningTotal = Vector3.zero;
    private Vector3 _runningTotal2 = Vector3.zero;
    private float _speedTotal;

    void FixedUpdate() {
        if (Cursor.lockState == CursorLockMode.None)
            return;

        //React to controls. (WASD, EQ and Mouse)
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        float moveUp = Input.GetKey(KeyCode.E) ? 1 : Input.GetKey(KeyCode.Q) ? -1 : 0;

        _posHistory.Enqueue(new Vector3(moveHorizontal, moveUp, moveVertical));
        _runningTotal2.x += moveHorizontal;
        _runningTotal2.y += moveUp;
        _runningTotal2.z += moveVertical;

        if (_posHistory.Count > 12)
        {
            _runningTotal2 -= _posHistory.Dequeue();
        }

        moveHorizontal = _runningTotal2.x / _posHistory.Count;
        moveUp = _runningTotal2.y / _posHistory.Count;
        moveVertical = _runningTotal2.z / _posHistory.Count;

        normalSpeed += Input.GetAxis("Mouse ScrollWheel") * minSpeed * 10;
        normalSpeed = Mathf.Min(maxSpeed, Mathf.Max(minSpeed, normalSpeed));

        float speed = normalSpeed;

        if (Input.GetKey(KeyCode.C)) {
            speed /= 10; ;
        } else if (Input.GetKey(KeyCode.LeftShift)) {
            speed *= 5;
        }

        _speedHistory.Enqueue(speed);
        _speedTotal += speed;

        if (_speedHistory.Count > 12)
        {
            _speedTotal -= _speedHistory.Dequeue();
        }

        speed = _speedTotal / _speedHistory.Count / 3;


        //Camera.gameObject.transform.Translate(new Vector3(moveHorizontal * speed * Time.deltaTime, moveUp * speed * Time.deltaTime, moveVertical * speed * Time.deltaTime));

        Vector3 move = Camera.main.transform.forward * moveVertical * speed * Time.deltaTime + Camera.main.transform.right * moveHorizontal * speed * Time.deltaTime;

        player.transform.Translate(move);

        Vector3 rot = Camera.gameObject.transform.eulerAngles;

        _rotHistory.Enqueue(new Vector3(-Input.GetAxis("Mouse Y") * 1.5f, Input.GetAxis("Mouse X") * 1.5f, 0));
        _runningTotal.x -= Input.GetAxis("Mouse Y") * 1.5f;
        _runningTotal.y += Input.GetAxis("Mouse X") * 1.5f;

        if (_rotHistory.Count > 12)
        {
            _runningTotal -= _rotHistory.Dequeue();
        }

        rot += _runningTotal / _rotHistory.Count * Time.deltaTime * 30;
        Camera.gameObject.transform.eulerAngles = rot;
    }
}
