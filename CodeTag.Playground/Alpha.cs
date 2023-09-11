namespace CodeTag.Playground;


[EnableCodeTag]
public class Robot
{
    public string Model { get; set; }
    public string SerialNumber { get; set; }

    [CodeTag("ROBOT-MOVE")]
    public void MoveForward(int distance)
    {
        Console.WriteLine($"{Model} moving forward by {distance} units.");
    }

    [CodeTag("ROBOT-STOP")]
    public void Stop()
    {
        Console.WriteLine($"{Model} has stopped.");
    }
}

public class RobotController
{
    public void Move(int distance)
    {
      // ... code omitted
    }

}
