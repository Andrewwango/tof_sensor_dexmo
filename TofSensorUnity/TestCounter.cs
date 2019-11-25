using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCounter : MonoBehaviour
{
    public static int TestCount = 0;
    private static int _testPrev = 0;

    public static int TestCount2 = 0;
    private static int _testPrev2 = 0;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (TestCount != _testPrev)
        {
            _testPrev = TestCount;
        }        

        if (TestCount2 != _testPrev2)
        {
            _testPrev2 = TestCount2;
        }        
    }


}
