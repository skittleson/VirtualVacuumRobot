using System;
using System.Threading;

namespace VirtualVacuumRobot {

    public static class Program {

        private static void Main(string[] args) {
            // var vacuum = new VacuumController(new ConsoleLogger(), true);
            // vacuum.RuntimeLoop();

            var queue = new QueueBroker();

            while (true) {
                Thread.Sleep(1000);
                foreach (var item in queue.ReceiveMessages()) {
                    Console.Out.WriteLine(item);
                }
            }
        }
    }
}