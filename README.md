# Building a Broker using C# and ArchBench v2 software

Este projeto consiste na construção de um Broker em C# que consiste num padrão arquitetural utilizado para estruturar sistemas distribuidos. Este é responsável pela coordenação da comunicação, tais como o encaminhamento de pedidos, bem como pela transmissão de resultados e excepções.

## ArchBench

O ArchBench é um software, que permite criar servidores locais com o foco de realizar testes. Todas as funcionalidades são introduzidas por Plugins, podendo retirar e adicionar diferentes plugins em cada servidor.

## Plugin Broker

Será inserido num servidor, que irá fazer a gestão de clientes entre servidores. Utilizando este plugin, os clientes irão apenas comunicar com o IP fornecido pelo Broker, e de seguida o Broker irá realizar os pedidos requisitados pelo cliente, fazendo pedidos aos servidores disponiveis e registados, funcionando como um intermediário. 

## Plugin Register
Será inserido num servidor em que irá ser responsável por criar uma ligação entre servidores, sendo necessário para registar como um servidor disponivel para o Broker.
Este plugin tera que ser editado nas settings, pois por defeito inicia-se na porta 8081

## Plugin Login
Será inserido num servidor junto com o Plugin Register, que será responsavel pelo registo do utilizador, enviando de volta uma Cookie que será o ID da variável de sessão. Será o Plugin principal.

## Plugin HtmlExample

Será inserido num servidor junto com o Plugin Register, em que irá fornecer recursos como ficheiros HTML (com formulários), imagens, videos. O cliente deverá começar a utilização atraves do url "/htmlex" para usufruir de uma interface, em que irá disponibilizar todos os recursos disponiveis
