using Amazon.SimpleNotificationService;
using System;
using System.Threading;

namespace VirtualVacuumRobot {

    public static class Program {

        private static void Main(string[] args) {
            var vacuum = new VacuumController(new ConsoleLogger(), new AmazonSimpleNotificationServiceClient(), new QueueBroker(), true);
            vacuum.RuntimeLoop(false);
        }
    }
}