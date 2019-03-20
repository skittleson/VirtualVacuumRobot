using Amazon.SimpleNotificationService;
using System;
using System.Threading;

namespace VirtualVacuumRobot {

    public static class Program {

        private static void Main(string[] args) {
            var logger = new ConsoleLogger();
            // random start up
            logger.Log("Random start up time!");
            var rnd = new Random();
            Thread.Sleep(rnd.Next(0,10) * 1000);
            var vacuum = new VacuumController(
                logger,
                new AmazonSimpleNotificationServiceClient(),
                new QueueBroker(),
                true);
            vacuum.RuntimeLoop();
        }
    }
}