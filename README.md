# 🏥 DicomSync - Editor e Envio DICOM

![Status do Projeto](https://img.shields.io/badge/Status-Em_Desenvolvimento-yellow)
![.NET](https://img.shields.io/badge/.NET-WPF-purple)
![License](https://img.shields.io/badge/License-MIT-blue)

**DicomSync** é uma aplicação desktop desenvolvida em **WPF (C#)** com interface moderna e minimalista (estilo Aero/Flat). O objetivo da ferramenta é facilitar a gestão, edição (correção de dados) e envio de imagens médicas (DICOM) para servidores PACS.

---

## 📸 Screenshots

*(Coloque aqui uma imagem da tela principal do software)*
![Interface Principal](https://via.placeholder.com/800x500?text=Screenshot+DicomSync)

---

## 🚀 Funcionalidades Principais

### 1. Gerenciamento de Estudos
* **Importação de Pastas:** Varredura recursiva de diretórios para localizar arquivos `.dcm`.
* **Visualização de Metadados:** Exibição rápida de Nome, ID, Accession Number, Data do Estudo e Descrição.
* **Listagem Organizada:** Visualização de imagens individuais ou agrupadas por Séries.

### 2. ✏️ DATAMAKER (Edição Inteligente)
Ferramenta poderosa para correção ou alteração de dados do paciente (Tags DICOM).
* **Edição em Lote:** Altera todos os arquivos do estudo de uma vez.
* **Backup Automático:** Antes de qualquer alteração, o sistema cria automaticamente uma pasta `BACKUP_ORIGINAL` com os arquivos intactos.
* **Feedback Visual:** Barras de progresso duplas (Backup e Atualização) para acompanhar o processo.

### 3. 📡 Conectividade PACS
* **C-ECHO (Ping):** Botão dedicado para testar a conectividade com o servidor PACS antes do envio.
* **C-STORE (Envio):** Envio robusto de imagens selecionadas ou séries completas.
* **Configuração Flexível:** Definição fácil de IP, Porta, AE Title Local e Remoto.

### 4. 🎨 Interface Moderna
* **WindowChrome:** Janela sem bordas padrão do Windows, com sombra projetada e cantos arredondados.
* **Responsividade:** Layout fluido que se adapta ao conteúdo, com suporte a redimensionamento.

---

## 🚧 Roadmap e Melhorias Futuras

O projeto está em evolução constante. Abaixo estão as funcionalidades planejadas para as próximas versões:

* **[ ] Logs Visuais Detalhados:** A aba "Logs" atualmente aguarda implementação. O objetivo é exibir um console em tempo real com detalhes das operações de I/O, erros de rede e logs da biblioteca `fo-dicom` para facilitar o diagnóstico.
* **[ ] Anonimização Automática:** Implementação de um modo de envio "Anonimizado". Ao ativar esta opção, o sistema removerá ou mascarará automaticamente dados sensíveis (Nome, PatientID, Data de Nascimento) antes de enviar para o PACS, ideal para uso em pesquisa e ensino.

---

## 🛠️ Tecnologias Utilizadas

* **Linguagem:** C#
* **Framework:** .NET (WPF)
* **Biblioteca DICOM:** [fo-dicom](https://github.com/fo-dicom/fo-dicom) (Versão 5.0+)
    * Utiliza `DicomClientFactory` para instanciar conexões modernas.
    * Implementa `FileReadOption.ReadAll` para evitar bloqueio de arquivos durante a edição.

---

## 📦 Como Usar

1.  **Configurar Rede:**
    * Preencha os dados do PACS (IP, Porta, AE Title) no topo da tela.
    * Clique no botão **📶** para testar a conexão (C-ECHO).

2.  **Carregar Estudo:**
    * Clique em `📂 Importar Estudo` e selecione a pasta raiz.
    * Clique em `🔍 Localizar` para carregar as imagens na memória.

3.  **Editar Dados (Opcional):**
    * Vá na aba "Dados do Paciente".
    * Clique no botão roxo **DATAMAKER**.
    * Altere os dados desejados e clique em "Salvar Alterações".
    * *O sistema fará o backup e salvará as alterações no disco.*

4.  **Enviar para o PACS:**
    * Selecione as imagens ou séries desejadas na lista.
    * Clique em `ENVIAR SELECIONADOS PARA O PACS`.

---

## 🔧 Instalação e Execução

### Pré-requisitos
* Visual Studio 2022 ou superior.
* .NET Desktop Runtime instalado.

### Passos
1.  Clone este repositório:
    ```bash
    git clone [https://github.com/SEU-USUARIO/DicomSync.git](https://github.com/SEU-USUARIO/DicomSync.git)
    ```
2.  Abra a solução no Visual Studio.
3.  Restaure os pacotes NuGet.
4.  Compile e execute (F5).

---

## 🤝 Contribuição

Contribuições são bem-vindas! Sinta-se à vontade para abrir Issues ou enviar Pull Requests.

1.  Faça um Fork do projeto.
2.  Crie uma Branch para sua Feature (`git checkout -b feature/NovaFuncionalidade`).
3.  Faça o Commit (`git commit -m 'Adicionando nova funcionalidade'`).
4.  Faça o Push (`git push origin feature/NovaFuncionalidade`).
5.  Abra um Pull Request.

---

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

---

**Desenvolvido com ❤️ para agilizar fluxos de Engenharia Clínica e Radiologia.**