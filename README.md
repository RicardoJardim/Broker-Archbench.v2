# Broker na utilização do ArchBench v2

Construção do plugin in broker no ambito da cadeira Arquitetura de Software

## Plugin Broker

Será inserido num servidor proprio e isolado, em que espera que através do plugin Register, um servidor se registe ao Broker para realizar os seus serviços

Com a utilização das cookies, é guardaddo um key/value com o nome "session_id" e com o valor do id da sessao, atraves da cookie o cliente fala sempre com o mesmo servidor (atraves do broker), assim distribuindo os clientes de forma equitativa pelos servidores

## Plugin Register

Será inserido num servidor em que irá ter outros plugin para responder aos pedidos do broker que por sua vez pedidos pelo cliente

Este plugin tera que ser editado nas settings, pois por defeito inicia-se na porta 8081

## Plugin HtmlExample

Será inserido num servidor junto com o Plugin Register, em que irá fornecer recursos como ficheiros HTML e ficheiros e pro fim responder a formulário.

O cliente deverá começar a utilização atraves do url "/htmlex" para usufruir de uma interface, em que irá disponibilizar todos os recursos disponiveis