// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

'use strict';

const Protocol = require('azure-iot-device-mqtt').Mqtt;
// Uncomment one of these transports and then change it in fromConnectionString to test other transports
// const Protocol = require('azure-iot-device-amqp').AmqpWs;
// const Protocol = require('azure-iot-device-http').Http;
// const Protocol = require('azure-iot-device-amqp').Amqp;
// const Protocol = require('azure-iot-device-mqtt').MqttWs;
const Client = require('azure-iot-device').Client;
const Message = require('azure-iot-device').Message;

// String containing Hostname, Device Id & Device Key in the following formats:
//  "HostName=<iothub_host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>"
const deviceConnectionString = "";
let sendInterval;

function disconnectHandler () {
  clearInterval(sendInterval);
  client.removeAllListeners();
  client.open().catch((err) => {
    console.error(err.message);
  });
}

// The AMQP and HTTP transports have the notion of completing, rejecting or abandoning the message.
// For example, this is only functional in AMQP and HTTP:
// client.complete(msg, printResultFor('completed'));
// If using MQTT calls to complete, reject, or abandon are no-ops.
// When completing a message, the service that sent the C2D message is notified that the message has been processed.
// When rejecting a message, the service that sent the C2D message is notified that the message won't be processed by the device. the method to use is client.reject(msg, callback).
// When abandoning the message, IoT Hub will immediately try to resend it. The method to use is client.abandon(msg, callback).
// MQTT is simpler: it accepts the message by default, and doesn't support rejecting or abandoning a message.
function messageHandler (msg) {
  console.log('Id: ' + msg.messageId + ' Body: ' + msg.data);
  client.complete(msg, printResultFor('completed'));
}

function generateMessage () {
  const deviceType = 'GrindingSensor' //change this to GrindingSensor | FanningSensor | MouldingSensor
  const fanSpeed = 10 + (Math.random() * 4); // range: [10, 14]
  const temperature = 200 + (Math.random() * 10); // range: [200, 300]
  const powerUsage = 60 + (Math.random() * 20); // range: [60, 80]
  const force = 300 + (Math.random() * 10); // range: [300, 400]
  const vibration = 80 + Math.floor(Math.random() * Math.floor(80)); // range: [99, 199]
  const roastingTime = 30 + (Math.floor(Math.random() * 100)); //range [30, 50]
  const data = JSON.stringify({ DeviceType: deviceType, FanSpeed: fanSpeed, Force: force, ChasisTemperature: temperature, PowerUsage: powerUsage, Vibration:vibration, RoastingTime: roastingTime });
  const message = new Message(data);
  message.properties.add('vibrationAlert', (vibration > 200) ? 'true' : 'false');
  message.contentType = "application/json";
  message.contentEncoding = "utf-8";
  return message;
}

function errorCallback (err) {
  console.error(err.message);
}

function connectCallback () {
  console.log('Client connected');
  // Create a message and send it to the IoT Hub every two seconds
  sendInterval = setInterval(() => {
    const message = generateMessage();
    console.log('Sending message: ' + message.getData());
    client.sendEvent(message, printResultFor('send'));
  }, 2000);

}

// fromConnectionString must specify a transport constructor, coming from any transport package.
let client = Client.fromConnectionString(deviceConnectionString, Protocol);

client.on('connect', connectCallback);
client.on('error', errorCallback);
client.on('disconnect', disconnectHandler);
client.on('message', messageHandler);

client.open()
.catch(err => {
  console.error('Could not connect: ' + err.message);
});

// Helper function to print results in the console
function printResultFor(op) {
  return function printResult(err, res) {
    if (err) console.log(op + ' error: ' + err.toString());
    if (res) console.log(op + ' status: ' + res.constructor.name);
  };
}
