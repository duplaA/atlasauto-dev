document.addEventListener('DOMContentLoaded', () => {
    const displayNameInput = document.getElementById('display-name');
    const postContent = document.getElementById('post-content');
    const submitButton = document.getElementById('submit-post');
    const postsContainer = document.getElementById('posts-container');

    if (sessionStorage.getItem('displayName')) {
        displayNameInput.value = sessionStorage.getItem('displayName');
    }

    displayNameInput.addEventListener('input', () => {
        sessionStorage.setItem('displayName', displayNameInput.value);
    });

    function loadPosts() {
        const posts = JSON.parse(localStorage.getItem('posts')) || [];
        postsContainer.innerHTML = '';
        // Sort posts in reverse chronological order
        posts.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp));
        posts.forEach(post => {
            const postElement = document.createElement('div');
            postElement.classList.add('post');

            const header = document.createElement('div');
            header.classList.add('post-header');

            const name = document.createElement('span');
            name.classList.add('post-name');
            name.textContent = post.name;

            const time = document.createElement('span');
            time.classList.add('post-time');
            time.textContent = new Date(post.timestamp).toLocaleString();

            header.appendChild(name);
            header.appendChild(time);

            const content = document.createElement('div');
            content.classList.add('post-content');
            content.innerHTML = linkify(post.content);

            postElement.appendChild(header);
            postElement.appendChild(content);
            postsContainer.appendChild(postElement);
        });
    }

    function linkify(text) {
        const urlRegex = /(https?:\/\/[^\s]+)/g;
        return text.replace(urlRegex, url => `<a href="${url}" target="_blank" rel="noopener noreferrer">${url}</a>`);
    }

    submitButton.addEventListener('click', () => {
        const name = displayNameInput.value.trim();
        const content = postContent.value.trim();

        if (!name || !content) {
            alert('Please provide a display name and post content.');
            return;
        }

        const posts = JSON.parse(localStorage.getItem('posts')) || [];
        const newPost = {
            name,
            content,
            timestamp: new Date().toISOString()
        };
        posts.push(newPost);
        localStorage.setItem('posts', JSON.stringify(posts));

        postContent.value = '';
        loadPosts();
    });

    loadPosts();
});