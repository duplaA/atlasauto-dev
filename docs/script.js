// === CONFIG ===
const SUPABASE_URL = 'https://rikenjfgogyhdhjcasse.supabase.co';
const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InJpa2VuamZnb2d5aGRoamNhc3NlIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NjMyMDU0MTcsImV4cCI6MjA3ODc4MTQxN30.bijOb9mbVA1sXePkRI7mRHMuv1GR8v_Bj0HTBab8Thw';
const PASSWORD = 'duszaverseny2025';
const GITHUB_REPO = 'duplaA/atlasauto'; // CASE SENSITIVE! Use exact repo name from GitHub
const GITHUB_TOKEN = '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'; // Optional: Add GitHub token for higher rate limits

const { createClient } = supabase;
const _supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY);

// User ID
function getUserId() {
    let id = localStorage.getItem('atlas_user_id');
    if (!id) {
        id = crypto.randomUUID();
        localStorage.setItem('atlas_user_id', id);
    }
    return id;
}
const USER_ID = getUserId();

// State
let allPosts = [];
let filteredPosts = [];
let githubData = {
    commits24h: 0,
    latestRelease: 'N/A',
    lastCommit: 'N/A',
    recentCommits: [],
    issues: []
};
let activeFilters = {
    search: '',
    author: null,
    tags: [],
    view: 'all' // 'all', 'milestones', 'issues'
};

// Detect mobile
const isMobile = window.innerWidth <= 768;

document.addEventListener('DOMContentLoaded', () => {
    initializeApp();
});

function initializeApp() {
    // Elements
    const fab = document.getElementById('fab');
    const mobileFab = document.getElementById('mobile-fab');
    const composerModal = document.getElementById('composer-modal');
    const passwordModal = document.getElementById('password-modal');
    const closeComposer = document.getElementById('close-composer');
    const cancelPassword = document.getElementById('cancel-password');
    const confirmPassword = document.getElementById('confirm-password');
    const passwordInput = document.getElementById('password-input');
    const displayNameInput = document.getElementById('display-name');
    const postContent = document.getElementById('post-content');
    const submitPost = document.getElementById('submit-post');
    const milestoneCheckbox = document.getElementById('milestone-checkbox');
    const searchInput = document.getElementById('search-input');
    const clearSearch = document.getElementById('clear-search');
    const activeFiltersContainer = document.getElementById('active-filters');
    const dashboardToggle = document.getElementById('dashboard-toggle');
    const dashboard = document.getElementById('dashboard');
    const exportBtn = document.getElementById('export-changelog');

    // Mobile navigation
    const navItems = document.querySelectorAll('.nav-item[data-page]');
    const mobilePages = document.querySelectorAll('.mobile-page');

    // Quick filters
    const filterBtns = document.querySelectorAll('.filter-btn[data-filter]');

    let pendingPost = null;
    let postsChart = null;

    // Autofill name
    const savedName = localStorage.getItem('atlas_display_name');
    if (savedName) {
        displayNameInput.value = savedName;
        if (document.getElementById('profile-name-input')) {
            document.getElementById('profile-name-input').value = savedName;
        }
        if (document.getElementById('profile-avatar')) {
            document.getElementById('profile-avatar').textContent = savedName[0].toUpperCase();
        }
    }

    displayNameInput.addEventListener('input', () => {
        localStorage.setItem('atlas_display_name', displayNameInput.value);
    });

    // Profile management
    if (document.getElementById('profile-name-input')) {
        document.getElementById('profile-name-input').addEventListener('input', (e) => {
            localStorage.setItem('atlas_display_name', e.target.value);
            document.getElementById('profile-avatar').textContent = e.target.value[0]?.toUpperCase() || 'U';
        });

        document.getElementById('save-profile')?.addEventListener('click', () => {
            const name = document.getElementById('profile-name-input').value.trim();
            if (name) {
                localStorage.setItem('atlas_display_name', name);
                alert('Profile saved! ✨');
            }
        });

        document.getElementById('export-user-data')?.addEventListener('click', () => {
            exportUserData();
        });
    }

    // Open composer
    fab?.addEventListener('click', () => composerModal.classList.remove('hidden'));
    mobileFab?.addEventListener('click', () => composerModal.classList.remove('hidden'));

    closeComposer.addEventListener('click', () => {
        composerModal.classList.add('hidden');
        milestoneCheckbox.checked = false;
    });

    cancelPassword.addEventListener('click', () => {
        passwordModal.classList.add('hidden');
        pendingPost = null;
    });

    // Close modals on backdrop click
    [composerModal, passwordModal].forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                modal.classList.add('hidden');
                if (modal === composerModal) milestoneCheckbox.checked = false;
            }
        });
    });

    // Submit → show password modal
    submitPost.addEventListener('click', () => {
        const name = displayNameInput.value.trim();
        const content = postContent.value.trim();
        if (!name || !content) return alert('Name and content required');

        pendingPost = {
            name,
            content,
            is_milestone: milestoneCheckbox.checked,
            is_issue: false
        };
        composerModal.classList.add('hidden');
        passwordModal.classList.remove('hidden');
        setTimeout(() => passwordInput.focus(), 100);
    });

    // Confirm password
    confirmPassword.addEventListener('click', async () => {
        if (!pendingPost) return;
        const input = passwordInput.value;
        if (input !== PASSWORD) {
            alert('Incorrect password');
            return;
        }

        const { error } = await _supabase
            .from('posts')
            .insert({
                name: pendingPost.name,
                content: pendingPost.content,
                is_milestone: pendingPost.is_milestone || false,
                is_issue: pendingPost.is_issue || false,
                reactions: { like: [], celebrate: [], rocket: [], eyes: [], perfect: [] }
            });

        if (error) {
            alert('Post failed: ' + error.message);
        } else {
            postContent.value = '';
            milestoneCheckbox.checked = false;
            passwordModal.classList.add('hidden');
            passwordInput.value = '';
            pendingPost = null;
            loadPosts();
        }
    });

    // Enter key in password
    passwordInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') confirmPassword.click();
    });

    // Search functionality
    searchInput.addEventListener('input', (e) => {
        activeFilters.search = e.target.value.trim().toLowerCase();
        clearSearch.classList.toggle('hidden', !activeFilters.search);
        applyFilters();
    });

    clearSearch.addEventListener('click', () => {
        searchInput.value = '';
        activeFilters.search = '';
        clearSearch.classList.add('hidden');
        applyFilters();
    });

    // Handle dashboard toggle separately
    dashboardToggle?.addEventListener('click', () => {
        dashboard.classList.toggle('hidden');
        dashboardToggle.classList.toggle('active');
        if (!dashboard.classList.contains('hidden')) {
            updateDashboard();
        }
    });

    // Handle view filters ('all', 'milestones', 'issues')
    filterBtns.forEach(btn => {
        if (btn.dataset.filter === 'dashboard') {
            return; // Skip dashboard button, it has its own listener
        }
        btn.addEventListener('click', () => {
            const filter = btn.dataset.filter;

            // Deactivate other view filters
            filterBtns.forEach(b => {
                if (b.dataset.filter !== 'dashboard') {
                    b.classList.remove('active');
                }
            });
            btn.classList.add('active');
            activeFilters.view = filter;
            applyFilters();
        });
    });

    // Mobile navigation
    navItems.forEach(item => {
        item.addEventListener('click', () => {
            const page = item.dataset.page;

            // Update active nav item
            navItems.forEach(n => n.classList.remove('active'));
            item.classList.add('active');

            // Show corresponding page
            mobilePages.forEach(p => p.classList.remove('active'));
            document.getElementById(`page-${page}`)?.classList.add('active');

            // Load specific content
            if (page === 'issues') {
                renderIssues();
            } else if (page === 'github') {
                updateGitHubMobile();
            } else if (page === 'profile') {
                updateProfile();
            }
        });
    });

    // Export changelog
    exportBtn?.addEventListener('click', () => {
        generateChangelog();
    });

    // Load posts and GitHub data
    loadPosts();
    fetchGitHubData();

    // Set interval for GitHub data refresh (every 5 minutes)
    setInterval(fetchGitHubData, 5 * 60 * 1000);

    // Real-time subscription
    _supabase
        .channel('posts')
        .on('postgres_changes', { event: '*', schema: 'public', table: 'posts' }, () => loadPosts())
        .subscribe();
}

// Load posts
async function loadPosts() {
    const { data: posts, error } = await _supabase
        .from('posts')
        .select('*')
        .order('timestamp', { ascending: false });

    if (error) {
        console.error(error);
        return;
    }

    allPosts = posts;
    applyFilters();
}

// Apply filters
function applyFilters() {
    filteredPosts = allPosts.filter(post => {
        // View filter
        if (activeFilters.view === 'milestones') {
            if (!post.is_milestone) return false;
        } else if (activeFilters.view === 'issues') {
            if (!post.is_issue) return false;
        } else { // 'all' view
            if (post.is_issue) return false; // Exclude issues from the 'all' feed
        }

        // Search filter
        if (activeFilters.search) {
            const searchLower = activeFilters.search;
            const matchesContent = post.content.toLowerCase().includes(searchLower);
            const matchesName = post.name.toLowerCase().includes(searchLower);
            if (!matchesContent && !matchesName) return false;
        }

        // Author filter
        if (activeFilters.author && post.name !== activeFilters.author) {
            return false;
        }

        // Tag filters
        if (activeFilters.tags.length > 0) {
            const postTags = extractHashtags(post.content);
            const hasAllTags = activeFilters.tags.every(tag =>
                postTags.includes(tag.toLowerCase())
            );
            if (!hasAllTags) return false;
        }

        return true;
    });

    renderPosts();
    updateActiveFiltersUI();
}

// Render posts
function renderPosts() {
    const containers = [
        document.getElementById('posts-container'),
        document.getElementById('posts-container-desktop')
    ];

    containers.forEach(container => {
        if (!container) return;
        container.innerHTML = '';

        if (filteredPosts.length === 0) {
            const message = activeFilters.view === 'issues' ? 'No open issues found' : 'No posts found';
            container.innerHTML = `<div class="loading">${message}</div>`;
            return;
        }

        filteredPosts.forEach((post, i) => {
            // All filtering is now done in applyFilters(). This function just renders.
            container.appendChild(createPostElement(post, i));
        });
    });
}

// Render issues
function renderIssues() {
    const container = document.getElementById('issues-container');
    if (!container) return;

    container.innerHTML = '';
    const issues = allPosts.filter(p => p.is_issue);

    if (issues.length === 0) {
        container.innerHTML = '<div class="loading">No issues reported</div>';
        return;
    }

    issues.forEach((post, i) => {
        container.appendChild(createPostElement(post, i));
    });
}

// Create post element
function createPostElement(post, index) {
    const postEl = document.createElement('div');
    postEl.className = 'post';
    if (post.is_milestone) postEl.classList.add('milestone');
    if (post.is_issue) postEl.classList.add('issue');
    postEl.style.animationDelay = `${index * 0.05}s`;

    // Avatar
    const avatar = document.createElement('div');
    avatar.className = 'avatar';
    if (post.is_issue) avatar.classList.add('system');
    avatar.textContent = post.name[0].toUpperCase();
    avatar.onclick = (e) => {
        e.stopPropagation();
        if (!post.is_issue) setAuthorFilter(post.name);
    };

    // Header
    const header = document.createElement('div');
    header.className = 'post-header';
    header.appendChild(avatar);

    const nameSpan = document.createElement('span');
    nameSpan.className = 'post-name';
    nameSpan.textContent = post.name;

    // Badges
    if (post.is_issue) {
        const badge = document.createElement('span');
        badge.className = 'system-badge';
        badge.textContent = '🤖 System';
        nameSpan.appendChild(badge);
    }

    if (post.is_milestone) {
        const badge = document.createElement('span');
        badge.className = 'milestone-badge';
        badge.innerHTML = `
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
                <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"></polygon>
            </svg>
            <span>Milestone</span>
        `;
        nameSpan.appendChild(badge);
    }

    const timeSpan = document.createElement('span');
    timeSpan.className = 'post-time';
    timeSpan.textContent = formatTimestamp(post.timestamp);

    header.appendChild(nameSpan);
    header.appendChild(timeSpan);

    // Content
    const content = document.createElement('div');
    content.className = 'post-content';
    content.innerHTML = processContent(post.content);

    // Add hashtag click handlers
    content.querySelectorAll('.hashtag').forEach(tag => {
        tag.onclick = (e) => {
            e.stopPropagation();
            addTagFilter(tag.textContent);
        };
    });

    // Image if URL detected
    const imageUrl = extractImageUrl(post.content);
    if (imageUrl) {
        const img = document.createElement('img');
        img.className = 'post-image';
        img.src = imageUrl;
        img.alt = 'Post image';
        img.onerror = () => img.remove();
        content.appendChild(img);
    }

    // Reactions
    const reactions = post.reactions || {
        like: [],
        celebrate: [],
        rocket: [],
        eyes: [],
        perfect: []
    };

    const actions = document.createElement('div');
    actions.className = 'post-actions';

    const reactionTypes = [
        { key: 'like', emoji: '❤️', label: 'Like' },
        { key: 'celebrate', emoji: '🎉', label: 'Celebrate' },
        { key: 'rocket', emoji: '🚀', label: 'Exciting' },
        { key: 'eyes', emoji: '👀', label: 'Watching' },
        { key: 'perfect', emoji: '💯', label: 'Perfect' }
    ];

    reactionTypes.forEach(({ key, emoji, label }) => {
        const btn = document.createElement('button');
        btn.className = 'reaction-btn';
        const userReacted = reactions[key]?.includes(USER_ID);
        if (userReacted) btn.classList.add('active');

        btn.innerHTML = `
            <span class="emoji">${emoji}</span>
            <span>${reactions[key]?.length || 0}</span>
        `;
        btn.title = label;

        btn.onclick = async (e) => {
            e.stopPropagation();
            await toggleReaction(post.id, key, reactions);
        };

        actions.appendChild(btn);
    });

    postEl.appendChild(header);
    postEl.appendChild(content);
    postEl.appendChild(actions);

    return postEl;
}

// Toggle reaction
async function toggleReaction(postId, reactionKey, currentReactions) {
    const userReacted = currentReactions[reactionKey]?.includes(USER_ID);
    const newReactions = { ...currentReactions };

    if (userReacted) {
        newReactions[reactionKey] = currentReactions[reactionKey].filter(id => id !== USER_ID);
    } else {
        newReactions[reactionKey] = [...(currentReactions[reactionKey] || []), USER_ID];
    }

    const { error } = await _supabase
        .from('posts')
        .update({ reactions: newReactions })
        .eq('id', postId);

    if (!error) loadPosts();
}

// Process content
function processContent(text) {
    let processed = linkify(text);
    processed = highlightHashtags(processed);
    return processed;
}

// Linkify URLs
function linkify(text) {
    const urlRegex = /(https?:\/\/[^\s]+)/g;
    return text.replace(urlRegex, url =>
        `<a href="${url}" target="_blank" rel="noopener noreferrer" onclick="event.stopPropagation();">${url}</a>`
    );
}

// Highlight hashtags
function highlightHashtags(text) {
    const hashtagRegex = /#(\w+)/g;
    return text.replace(hashtagRegex, (match) =>
        `<span class="hashtag">${match}</span>`
    );
}

// Extract hashtags
function extractHashtags(text) {
    const hashtagRegex = /#(\w+)/g;
    const matches = text.match(hashtagRegex);
    return matches ? matches.map(tag => tag.toLowerCase()) : [];
}

// Extract image URL
function extractImageUrl(text) {
    const imageRegex = /(https?:\/\/[^\s]+\.(?:jpg|jpeg|png|gif|webp|svg))/i;
    const match = text.match(imageRegex);
    return match ? match[0] : null;
}

// Set author filter
function setAuthorFilter(author) {
    if (activeFilters.author === author) {
        activeFilters.author = null;
    } else {
        activeFilters.author = author;
    }
    applyFilters();
}

// Add tag filter
function addTagFilter(tag) {
    const tagLower = tag.toLowerCase();
    if (!activeFilters.tags.includes(tagLower)) {
        activeFilters.tags.push(tagLower);
        applyFilters();
    }
}

// Remove filter
function removeFilter(type, value) {
    if (type === 'author') {
        activeFilters.author = null;
    } else if (type === 'tag') {
        activeFilters.tags = activeFilters.tags.filter(t => t !== value);
    }
    applyFilters();
}

// Update active filters UI
function updateActiveFiltersUI() {
    const container = document.getElementById('active-filters');
    if (!container) return;

    container.innerHTML = '';

    if (activeFilters.author) {
        const chip = createFilterChip('author', `@${activeFilters.author}`, activeFilters.author);
        container.appendChild(chip);
    }

    activeFilters.tags.forEach(tag => {
        const chip = createFilterChip('tag', tag, tag);
        container.appendChild(chip);
    });
}

// Create filter chip
function createFilterChip(type, label, value) {
    const chip = document.createElement('div');
    chip.className = 'filter-chip';
    chip.innerHTML = `
        <span>${label}</span>
        <span class="filter-chip-close">×</span>
    `;
    chip.onclick = () => removeFilter(type, value);
    return chip;
}

// Format timestamp
function formatTimestamp(timestamp) {
    const date = new Date(timestamp);
    const now = new Date();
    const diff = now - date;
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);

    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m`;
    if (hours < 24) return `${hours}h`;
    if (days < 7) return `${days}d`;
    return date.toLocaleDateString();
}

// Fetch GitHub data
async function fetchGitHubData() {
    if (!GITHUB_REPO) {
        console.log('GitHub repo not configured');
        return;
    }

    try {
        const headers = GITHUB_TOKEN ? { 'Authorization': `token ${GITHUB_TOKEN}` } : {};

        // Fetch commits
        const commitsRes = await fetch(`https://api.github.com/repos/${GITHUB_REPO}/commits?per_page=100`, { headers });

        if (!commitsRes.ok) {
            console.error(`GitHub API error: ${commitsRes.status} ${commitsRes.statusText}`);
            console.log(`URL attempted: https://api.github.com/repos/${GITHUB_REPO}/commits`);
            console.log('Make sure GITHUB_REPO matches exactly (case-sensitive): username/repo-name');

            // Set fallback values
            githubData.commits24h = 0;
            githubData.lastCommit = 'Check repo name';
            updateGitHubUI();
            return;
        }

        const commits = await commitsRes.json();

        if (Array.isArray(commits)) {
            // Commits in last 24h
            const oneDayAgo = new Date(Date.now() - 24 * 60 * 60 * 1000);
            githubData.commits24h = commits.filter(c => new Date(c.commit.author.date) > oneDayAgo).length;

            // Last commit
            if (commits[0]) {
                const lastCommitDate = new Date(commits[0].commit.author.date);
                githubData.lastCommit = formatTimestamp(lastCommitDate);
            }

            githubData.recentCommits = commits.slice(0, 10);
        }

        // Fetch GitHub issues (open)
        const issuesRes = await fetch(`https://api.github.com/repos/${GITHUB_REPO}/issues?state=open&per_page=50`, { headers });

        if (issuesRes.ok) {
            const issues = await issuesRes.json();
            githubData.issues = issues.filter(issue => !issue.pull_request); // Filter out PRs
            console.log(`Found ${githubData.issues.length} open issues on GitHub`);

            // Sync issues to database
            await syncIssuesToDatabase(githubData.issues);
        } else {
            console.error(`GitHub issues API error: ${issuesRes.status}`);
        }

        // Fetch latest release (handle 404 gracefully if no releases)
        const releaseRes = await fetch(`https://api.github.com/repos/${GITHUB_REPO}/releases/latest`, { headers });

        if (releaseRes.ok) {
            const release = await releaseRes.json();
            if (release.tag_name) {
                githubData.latestRelease = release.tag_name;
            }
        } else if (releaseRes.status === 404) {
            // No releases yet - this is normal
            githubData.latestRelease = 'No releases';
        } else {
            console.error(`GitHub releases API error: ${releaseRes.status}`);
            githubData.latestRelease = 'Error';
        }

        updateGitHubUI();
        console.log('GitHub data loaded successfully:', githubData);
    } catch (error) {
        console.error('GitHub fetch error:', error);
        githubData.commits24h = 0;
        githubData.lastCommit = 'Error loading';
        githubData.latestRelease = 'Error';
        updateGitHubUI();
    }
}

// Sync GitHub issues to database
async function syncIssuesToDatabase(issues) {
    if (!issues || issues.length === 0) return;

    // Get existing issue posts
    const { data: existingPosts } = await _supabase
        .from('posts')
        .select('content')
        .eq('is_issue', true);

    const existingIssueNumbers = existingPosts?.map(p => {
        const match = p.content.match(/#(\d+)/);
        return match ? parseInt(match[1]) : null;
    }).filter(Boolean) || [];

    // Add new issues
    for (const issue of issues) {
        if (!existingIssueNumbers.includes(issue.number)) {
            const content = `Issue #${issue.number}: ${issue.title}\n\n${issue.body?.substring(0, 200) || 'No description'}${issue.body?.length > 200 ? '...' : ''}\n\nView on GitHub: ${issue.html_url}`;

            await _supabase.from('posts').insert({
                name: 'System',
                content: content,
                is_issue: true,
                is_milestone: false,
                reactions: { like: [], celebrate: [], rocket: [], eyes: [], perfect: [] }
            });

            console.log(`Added issue #${issue.number} to database`);
        }
    }
}

// Update GitHub UI
function updateGitHubUI() {
    // Desktop
    const commits24hEl = document.getElementById('commits-24h');
    const latestReleaseEl = document.getElementById('latest-release');
    const lastCommitEl = document.getElementById('last-commit');

    if (commits24hEl) commits24hEl.textContent = githubData.commits24h;
    if (latestReleaseEl) latestReleaseEl.textContent = githubData.latestRelease;
    if (lastCommitEl) lastCommitEl.textContent = githubData.lastCommit;

    // Mobile
    updateGitHubMobile();
}

// Update GitHub mobile
function updateGitHubMobile() {
    const mobileCommits = document.getElementById('mobile-commits-24h');
    const mobileRelease = document.getElementById('mobile-latest-release');
    const mobileCommit = document.getElementById('mobile-last-commit');

    if (mobileCommits) mobileCommits.textContent = githubData.commits24h;
    if (mobileRelease) mobileRelease.textContent = githubData.latestRelease;
    if (mobileCommit) mobileCommit.textContent = githubData.lastCommit;

    // Render activity timeline
    const timeline = document.getElementById('github-activity-timeline');
    if (timeline && githubData.recentCommits.length > 0) {
        timeline.innerHTML = '<div class="page-header" style="margin-top: 20px;"><h3 style="font-size: 1rem; color: #71767b;">Recent Commits</h3></div>';

        githubData.recentCommits.forEach(commit => {
            const commitEl = document.createElement('a');
            commitEl.href = commit.html_url;
            commitEl.target = '_blank';
            commitEl.rel = 'noopener noreferrer';
            commitEl.className = 'commit-card';
            commitEl.style.textDecoration = 'none';
            commitEl.style.color = 'inherit';
            commitEl.style.display = 'block';

            const shortSha = commit.sha.substring(0, 7);
            const message = commit.commit.message.split('\n')[0];
            const truncatedMessage = message.length > 60 ? message.substring(0, 60) + '...' : message;

            commitEl.innerHTML = `
                <div class="commit-header">
                    <div class="commit-avatar" style="width: 32px; height: 32px; border-radius: 50%; background: #238636; display: flex; align-items: center; justify-content: center; font-weight: 700; color: white; font-size: 0.8rem; flex-shrink: 0;">
                        ${commit.author?.login?.[0]?.toUpperCase() || 'G'}
                    </div>
                    <div class="commit-info" style="flex: 1; min-width: 0;">
                        <div class="commit-author" style="font-weight: 600; font-size: 0.9rem; color: #e7e9ea;">${commit.commit.author.name}</div>
                        <div class="commit-message" style="font-size: 0.85rem; color: #71767b; margin-top: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${truncatedMessage}</div>
                    </div>
                    <div class="commit-meta" style="display: flex; flex-direction: column; align-items: flex-end; gap: 4px; flex-shrink: 0;">
                        <span class="commit-sha" style="font-family: monospace; font-size: 0.75rem; background: rgba(29, 155, 240, 0.15); color: #1d9bf0; padding: 2px 6px; border-radius: 6px;">${shortSha}</span>
                        <span class="commit-time" style="font-size: 0.75rem; color: #71767b;">${formatTimestamp(commit.commit.author.date)}</span>
                    </div>
                </div>
            `;

            timeline.appendChild(commitEl);
        });
    }
}

// Update dashboard
function updateDashboard() {
    // Posts this week
    const weekAgo = new Date();
    weekAgo.setDate(weekAgo.getDate() - 7);
    const postsThisWeek = allPosts.filter(p => new Date(p.timestamp) > weekAgo && !p.is_issue).length;
    document.getElementById('stat-posts-week').textContent = postsThisWeek;

    // Total milestones
    const milestones = allPosts.filter(p => p.is_milestone).length;
    document.getElementById('stat-milestones').textContent = milestones;

    // Open issues
    const issues = allPosts.filter(p => p.is_issue).length;
    document.getElementById('stat-issues').textContent = issues;

    // Most active user (excluding system)
    const userCounts = {};
    allPosts.filter(p => !p.is_issue).forEach(p => {
        userCounts[p.name] = (userCounts[p.name] || 0) + 1;
    });
    const mostActive = Object.entries(userCounts).sort((a, b) => b[1] - a[1])[0];
    document.getElementById('stat-active-user').textContent = mostActive ? mostActive[0] : '—';

    // Chart
    updatePostsChart();
}

// Update posts chart
function updatePostsChart() {
    const ctx = document.getElementById('posts-chart');
    if (!ctx) return;

    const last7Days = [];
    const postCounts = [];
    const today = new Date();

    for (let i = 6; i >= 0; i--) {
        const date = new Date(today);
        date.setDate(date.getDate() - i);
        const dateStr = date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        last7Days.push(dateStr);

        const dayStart = new Date(date.setHours(0, 0, 0, 0));
        const dayEnd = new Date(date.setHours(23, 59, 59, 999));
        const count = allPosts.filter(p => {
            const postDate = new Date(p.timestamp);
            return postDate >= dayStart && postDate <= dayEnd && !p.is_issue;
        }).length;
        postCounts.push(count);
    }

    if (window.postsChart) {
        window.postsChart.destroy();
    }

    window.postsChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: last7Days,
            datasets: [{
                label: 'Posts',
                data: postCounts,
                backgroundColor: 'rgba(29, 155, 240, 0.6)',
                borderColor: 'rgba(29, 155, 240, 1)',
                borderWidth: 2,
                borderRadius: 8
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1,
                        color: '#71767b'
                    },
                    grid: {
                        color: 'rgba(47, 51, 54, 0.5)'
                    }
                },
                x: {
                    ticks: { color: '#71767b' },
                    grid: { display: false }
                }
            }
        }
    });
}

// Update profile
function updateProfile() {
    const userPosts = allPosts.filter(p => {
        const savedName = localStorage.getItem('atlas_display_name');
        return savedName && p.name === savedName && !p.is_issue;
    });

    document.getElementById('user-posts-count').textContent = userPosts.length;

    // Count reactions given
    let reactionsCount = 0;
    allPosts.forEach(p => {
        if (p.reactions) {
            Object.values(p.reactions).forEach(arr => {
                if (arr.includes(USER_ID)) reactionsCount++;
            });
        }
    });
    document.getElementById('user-reactions-count').textContent = reactionsCount;
}

// Export user data
function exportUserData() {
    const savedName = localStorage.getItem('atlas_display_name');
    if (!savedName) {
        alert('No profile name set');
        return;
    }

    const userPosts = allPosts.filter(p => p.name === savedName && !p.is_issue);

    let data = `# My Atlas Auto Progress Data\n\n`;
    data += `Name: ${savedName}\n`;
    data += `User ID: ${USER_ID}\n`;
    data += `Total Posts: ${userPosts.length}\n\n`;
    data += `## My Posts\n\n`;

    userPosts.forEach(post => {
        data += `### ${new Date(post.timestamp).toLocaleString()}\n`;
        data += `${post.content}\n\n`;
    });

    const blob = new Blob([data], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `atlas-my-data-${Date.now()}.md`;
    a.click();
    URL.revokeObjectURL(url);
}

// Generate changelog
function generateChangelog() {
    const grouped = {};

    allPosts.filter(p => !p.is_issue).forEach(post => {
        const date = new Date(post.timestamp).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        });
        if (!grouped[date]) grouped[date] = [];
        grouped[date].push(post);
    });

    let markdown = '# Atlas Auto Progress - Changelog\n\n';
    markdown += `Generated on ${new Date().toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    })}\n\n`;
    markdown += '---\n\n';

    Object.entries(grouped).sort((a, b) => new Date(b[0]) - new Date(a[0])).forEach(([date, posts]) => {
        markdown += `## ${date}\n\n`;
        posts.forEach(post => {
            const prefix = post.is_milestone ? '⭐ **MILESTONE** ' : '- ';
            const content = post.content.replace(/\n/g, ' ');
            markdown += `${prefix}${content} _(by ${post.name})_\n`;
        });
        markdown += '\n';
    });

    // Download
    const blob = new Blob([markdown], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `atlas-changelog-${Date.now()}.md`;
    a.click();
    URL.revokeObjectURL(url);
}