#ChatAppMe
A modern chat application inspired by Facebook Messenger, designed to provide real-time messaging with a familiar and intuitive interface.

##Overview
ChatAppMe is a full-stack chat app that enables users to send and receive messages in real-time, closely resembling the look and feel of Facebook Messenger. The project began as a client-server application utilizing a standard REST API for message exchange. As the application evolved, I integrated a WebSocket server to enable seamless, instant messaging—bringing a true "live chat" experience to users.

##Features
User authentication (Sign up, log in)
1-on-1 chat conversations
Messenger-style UI (contact list, chat window)
Message history and persistence
Real-time messaging (powered by WebSockets)
Evolution
###V1:

Used a REST API for sending and retrieving messages
Laid the groundwork for app architecture and UI
###V2:

Integrated a WebSocket server for event-driven, low-latency communication
Achieved instant message delivery
Improved scalability and user experience
Tech Stack
Frontend: C# (Blazor Webassembly), CSS
Backend: C# (.net 9)
Database: MongoDB
Real-time Communication: C# (SignalR) (WebSockets)
Getting Started
Clone the repo and try it for yourself
