using Amazon.SimpleNotificationService;
using System;
using System.Threading;

namespace VirtualVacuumRobot {

    public static class Program {

        private static void Main(string[] args) {

            // random start up
            var rnd = new Random();
            Thread.Sleep(rnd.Next(0,10) * 1000);
            var vacuum = new VacuumController(
                new ConsoleLogger(),
                new AmazonSimpleNotificationServiceClient(),
                new QueueBroker(),
                true);
            vacuum.RuntimeLoop();
        }
    }
}